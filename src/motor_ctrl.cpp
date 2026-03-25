#include "motor_ctrl.h"
#include "uart_task.h"
#include <Arduino.h>
#include <board.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <freertos/queue.h>
#include <esp32-hal.h>
#include <cstdlib>

// TMC2209 runs in standalone STEP/DIR mode — no UART used.

// ─── Internal command queue ───────────────────────────────────────────────────
enum class CmdType : uint8_t { MOVE_ABS, MOVE_REL, GO_HOME, FIND_HOME, STOP };
struct MotorQueueCmd { CmdType type; int32_t arg; };
static QueueHandle_t s_queue;

// ─── Per-node state ───────────────────────────────────────────────────────────
static MotorStatus s_status = {
    .id          = 0,
    .position    = 0,
    .target      = 0,
    .state       = MotorState::MS_IDLE,
    .enabled     = true,
    .dirFlipped  = false,
    .microsteps  = 16,
    .currentMA   = 600,
    .speedUs     = 500,
    .accelSteps  = 0,
};

// ─── Helpers ─────────────────────────────────────────────────────────────────
static inline bool id_match(uint8_t id) { return id == s_status.id; }

// Issue one step pulse, updating position counter.
// dir: true = forward, false = reverse (before polarity flip).
static void do_step(bool dir) {
    bool physDir = dir ^ s_status.dirFlipped;
    digitalWrite(PIN_DIR, physDir ? HIGH : LOW);
    digitalWrite(PIN_STEP, HIGH);
    ets_delay_us(s_status.speedUs);
    digitalWrite(PIN_STEP, LOW);
    ets_delay_us(s_status.speedUs);
    s_status.position += (dir ? 1 : -1);
}

// Execute N steps with optional trapezoidal ramp.
static void run_steps(int32_t steps) {
    if (steps == 0) return;
    bool    dir   = (steps > 0);
    int32_t count = abs(steps);

    // Acceleration ramp: ramp up for first accelSteps, cruise, ramp down.
    // TODO: replace linear ramp with S-curve for smoother operation.
    uint32_t ramp = s_status.accelSteps ? s_status.accelSteps : 0;

    for (int32_t i = 0; i < count; i++) {
        if (ramp > 0) {
            // Linear speed ramp: start at 4× period, reach cruise at ramp end.
            uint32_t phase  = (i < (int32_t)ramp) ? i : (int32_t)ramp;
            uint32_t decel  = (count - 1 - i);
            if (decel < (int32_t)ramp) phase = decel;
            // period scales from 4× cruise down to 1× cruise
            uint32_t period = s_status.speedUs +
                              (s_status.speedUs * 3 * (ramp - phase)) / ramp;
            digitalWrite(PIN_DIR, (dir ^ s_status.dirFlipped) ? HIGH : LOW);
            digitalWrite(PIN_STEP, HIGH); ets_delay_us(period);
            digitalWrite(PIN_STEP, LOW);  ets_delay_us(period);
            s_status.position += (dir ? 1 : -1);
        } else {
            do_step(dir);
        }
        if ((i & 0x3F) == 0x3F) taskYIELD();
    }
}

// ─── FreeRTOS motor task ──────────────────────────────────────────────────────
static void motorTask(void *) {
    MotorQueueCmd cmd;
    for (;;) {
        if (xQueueReceive(s_queue, &cmd, portMAX_DELAY) != pdTRUE) continue;

        if (!s_status.enabled) {
            Serial.println("[motor] ERR: driver disabled");
            continue;
        }

        s_status.state = MotorState::MS_MOVING;

        switch (cmd.type) {
            case CmdType::MOVE_ABS: {
                int32_t dist     = cmd.arg - s_status.position;
                s_status.target  = cmd.arg;
                run_steps(dist);
                break;
            }
            case CmdType::MOVE_REL:
                s_status.target = s_status.position + cmd.arg;
                run_steps(cmd.arg);
                break;

            case CmdType::GO_HOME:
                s_status.target = 0;
                run_steps(-s_status.position);
                break;

            case CmdType::FIND_HOME:
                // TODO: full stall-guard homing via SGT threshold on TMC2209.
                // Current stub: move slowly until DIAG asserts (stall / endstop),
                // then zero the position counter.
                s_status.state = MotorState::MS_HOMING;
                {
                    const uint32_t saved = s_status.speedUs;
                    s_status.speedUs = 2000;  // slow approach
                    for (int i = 0; i < 50000; i++) {
                        if (digitalRead(PIN_DIAG) == LOW) break;
                        do_step(false);  // move in –direction toward home
                        if ((i & 0x3F) == 0x3F) taskYIELD();
                    }
                    s_status.speedUs  = saved;
                    s_status.position = 0;
                    s_status.target   = 0;
                }
                break;

            case CmdType::STOP:
                xQueueReset(s_queue);
                break;
        }

        if (s_status.state != MotorState::MS_HOMING) {
            s_status.target = s_status.position;
        }
        s_status.state = MotorState::MS_IDLE;
        Serial.printf("[motor] done pos=%ld\n", (long)s_status.position);
    }
}

// ─── Lifecycle ───────────────────────────────────────────────────────────────
void motor_ctrl_init() {

    // TMC2209 in standalone mode: configure via MS1/MS2 & VREF hardware pins.
    // No UART initialisation needed.
    pinMode(PIN_STEP_EN, OUTPUT);
    pinMode(PIN_STEP,    OUTPUT);
    pinMode(PIN_DIR,     OUTPUT);
    // PIN_DIAG configured as OUTPUT blink by uart_task_start()
    digitalWrite(PIN_STEP_EN, LOW);   // enable driver

    s_queue = xQueueCreate(16, sizeof(MotorQueueCmd));
    xTaskCreate(motorTask, "motor", 3072, nullptr, 1, nullptr);
}

// ─── Chain forward ── moved to uart_task.cpp ─────────────────────────────────

// ─── Identity ────────────────────────────────────────────────────────────────
void    motor_setID(uint8_t newID)  { s_status.id = newID & 0x0F; }
uint8_t motor_getID()               { return s_status.id; }

// ─── Motion ──────────────────────────────────────────────────────────────────
void motor_moveTo(uint8_t id, int32_t pos) {
    if (!id_match(id)) return;
    MotorQueueCmd c = { CmdType::MOVE_ABS, pos };
    xQueueSend(s_queue, &c, 0);
}

void motor_move(uint8_t id, int32_t dist) {
    if (!id_match(id)) return;
    MotorQueueCmd c = { CmdType::MOVE_REL, dist };
    xQueueSend(s_queue, &c, 0);
}

void motor_goHome(uint8_t id) {
    if (!id_match(id)) return;
    MotorQueueCmd c = { CmdType::GO_HOME, 0 };
    xQueueSend(s_queue, &c, 0);
}

void motor_findHome(uint8_t id) {
    if (!id_match(id)) return;
    MotorQueueCmd c = { CmdType::FIND_HOME, 0 };
    xQueueSend(s_queue, &c, 0);
}

void motor_stop(uint8_t id) {
    if (!id_match(id)) return;
    xQueueReset(s_queue);
    s_status.state  = MotorState::MS_IDLE;
    s_status.target = s_status.position;
}

// ─── Configuration ───────────────────────────────────────────────────────────
void motor_setPosition(uint8_t id, int32_t pos) {
    if (!id_match(id)) return;
    s_status.position = pos;
    s_status.target   = pos;
}

void motor_flipDir(uint8_t id) {
    if (!id_match(id)) return;
    s_status.dirFlipped = !s_status.dirFlipped;
}

void motor_setStepSize(uint8_t id, uint16_t ustep) {
    if (!id_match(id)) return;
    s_status.microsteps = ustep;
    // microstep config is hardware (MS1/MS2 pins) in standalone mode
}

void motor_setCurrent(uint8_t id, uint16_t mA) {
    if (!id_match(id)) return;
    s_status.currentMA = mA;
    // current set by VREF hardware in standalone mode
}

void motor_setSpeed(uint8_t id, uint32_t halfUs) {
    if (!id_match(id)) return;
    s_status.speedUs = halfUs;
}

void motor_setAccel(uint8_t id, uint32_t rampSteps) {
    if (!id_match(id)) return;
    s_status.accelSteps = rampSteps;
}

void motor_enable(uint8_t id, bool en) {
    if (!id_match(id)) return;
    s_status.enabled = en;
    digitalWrite(PIN_STEP_EN, en ? LOW : HIGH);
    s_status.state = en ? MotorState::MS_IDLE : MotorState::MS_DISABLED;
}

// ─── Query ───────────────────────────────────────────────────────────────────
bool motor_getStatus(uint8_t id, MotorStatus &out) {
    if (!id_match(id)) return false;
    out = s_status;
    return true;
}
