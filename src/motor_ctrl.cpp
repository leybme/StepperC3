#include "motor_ctrl.h"
#include "uart_task.h"
#include "board_prefs.h"
#include <Arduino.h>
#include <board.h>
#include <Preferences.h>
#include <FastAccelStepper.h>
#include <TMCStepper.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <esp32-hal.h>

// TMC2209 runs in standalone STEP/DIR mode — no UART used.

// ─── FastAccelStepper engine ──────────────────────────────────────────────────
static FastAccelStepperEngine s_engine;
static FastAccelStepper *s_stepper = nullptr;

// ─── Per-node state ───────────────────────────────────────────────────────────
static MotorStatus s_status = {
    .id = 0,
    .position = 0,
    .target = 0,
    .state = MotorState::MS_IDLE,
    .enabled = true,
    .dirFlipped = false,
    .microsteps = 16,
    .currentMA = 600,
    .speedHz = 1000,
    .accelHz = 1000,
};

static volatile bool s_homing_req = false;

// ─── Helpers ─────────────────────────────────────────────────────────────────
static inline bool id_match(uint8_t id) { return id == g_board_id; }

// ─── NVS persistence ─────────────────────────────────────────────────────────
// Only configuration fields are persisted; position/target/state reset each boot.
static const char *MOTOR_NS = "motor";

static void motor_prefs_save()
{
    Preferences prefs;
    prefs.begin(MOTOR_NS, false);
    prefs.putBool("dirFlip", s_status.dirFlipped);
    prefs.putUShort("microsteps", s_status.microsteps);
    prefs.putUShort("currentMA", s_status.currentMA);
    prefs.putULong("speedHz", s_status.speedHz);
    prefs.putULong("accelHz", s_status.accelHz);
    prefs.putBool("enabled", s_status.enabled);
    prefs.end();
}

static void motor_prefs_load()
{
    Preferences prefs;
    prefs.begin(MOTOR_NS, true);
    s_status.dirFlipped = prefs.getBool("dirFlip", false);
    s_status.microsteps = prefs.getUShort("microsteps", 16);
    s_status.currentMA = prefs.getUShort("currentMA", 600);
    s_status.speedHz = prefs.getULong("speedHz", 1000);
    s_status.accelHz = prefs.getULong("accelHz", 1000);
    s_status.enabled = prefs.getBool("enabled", true);
    prefs.end();
}

static void apply_speed_accel()
{
    if (!s_stepper)
        return;
    s_stepper->setSpeedInHz(s_status.speedHz ? s_status.speedHz : 1);
    s_stepper->setAcceleration(s_status.accelHz ? s_status.accelHz : 100);
}

// ─── Monitor / homing task ────────────────────────────────────────────────────
static void motorTask(void *)
{
    for (;;)
    {
        vTaskDelay(pdMS_TO_TICKS(10));
        if (!s_stepper)
            continue;

        // ── Homing sequence ───────────────────────────────────────────────────
        if (s_homing_req)
        {
            s_homing_req = false;
            s_status.state = MotorState::MS_HOMING;
            s_stepper->setSpeedInHz(200);
            s_stepper->setAcceleration(500);
            s_stepper->moveTo(-2000000L); // large negative target
            while (s_stepper->isRunning())
            {
                if (digitalRead(PIN_DIAG) == LOW)
                {
                    s_stepper->forceStopAndNewPosition(0);
                    s_status.position = 0;
                    s_status.target = 0;
                    break;
                }
                vTaskDelay(pdMS_TO_TICKS(1));
            }
            apply_speed_accel();
            s_status.state = MotorState::MS_IDLE;
            Serial.println("[motor] homing done");
            continue;
        }

        // ── Normal state tracking ─────────────────────────────────────────────
        if (s_status.state == MotorState::MS_MOVING && !s_stepper->isRunning())
        {
            s_status.position = s_stepper->getCurrentPosition();
            s_status.target = s_status.position;
            s_status.state = MotorState::MS_IDLE;
            Serial.printf("[motor] done pos=%ld\n", (long)s_status.position);
        }
    }
}

// ─── Lifecycle ───────────────────────────────────────────────────────────────
void motor_ctrl_init()
{
    motor_prefs_load();

    // ── Configure TMC2209 via UART (borrow Serial1 on TMC pins) ──────────────
    // Serial1 is free here — uart_task_start() has not been called yet.
    // After this block, Serial1.end() releases it; uart_task_start() will
    // re-init Serial1 on PIN_UART_OUT_TX/RX (IO21/IO20).
    Serial1.begin(115200, SERIAL_8N1, /*rx=*/-1, /*tx=*/TMC_PIN_RX);
    {
        TMC2209Stepper tmc(&Serial1, TMC_R_SENSE, /*addr=*/0);
        tmc.begin();
        tmc.pdn_disable(true);     // enable UART control; disable PDN_UART power-down
        tmc.I_scale_analog(false); // use UART-set current, not VREF pin
        tmc.rms_current(s_status.currentMA);
        
        // // test the stepper by rotating 1000 step then change the microstep to 16 and rotate back round 1000 steps ()
        // pinMode(PIN_STEP, OUTPUT);
        // pinMode(PIN_DIR, OUTPUT);
        // pinMode(PIN_STEP_EN, OUTPUT);
        // digitalWrite(PIN_STEP_EN, LOW); // enable driver (active LOW)
        // tmc.microsteps(8);
        // for (int i = 0; i < 3200; i++)
        // {
        //     //toggle step pin
        //     digitalWrite(PIN_STEP, !digitalRead(PIN_STEP));
        //     delay(1);
        // }
        // delay(500);
        // tmc.microsteps(32);
        // for (int i = 0; i < 3200; i++)
        // {
        //     digitalWrite(PIN_STEP, !digitalRead(PIN_STEP));
        //     delay(1);
        // }
        tmc.microsteps(s_status.microsteps);
        tmc.en_spreadCycle(false);      // StealthChop (quieter)
        tmc.shaft(s_status.dirFlipped); // apply saved direction polarity
        Serial.printf("[motor] TMC2209 cfg: %umA %u ustep dir=%d\n",
                      s_status.currentMA, s_status.microsteps,
                      (int)s_status.dirFlipped);
    }
    Serial1.end(); // release; uart_task_start() re-claims on chain pins
    // ─────────────────────────────────────────────────────────────────────────

    s_engine.init();
    s_stepper = s_engine.stepperConnectToPin(PIN_STEP);
    if (!s_stepper)
    {
        Serial.println("[motor] ERR: stepperConnectToPin failed");
        return;
    }
    // PIN_DIAG is owned by uart_task as a receive-blink output.
    s_stepper->setDirectionPin(PIN_DIR, !s_status.dirFlipped);
    s_stepper->setEnablePin(PIN_STEP_EN, true); // active LOW
    s_stepper->setAutoEnable(false);
    apply_speed_accel();

    if (s_status.enabled)
        s_stepper->enableOutputs();
    else
        s_stepper->disableOutputs();

    xTaskCreate(motorTask, "motor", 2048, nullptr, 1, nullptr);
}

// ─── Identity ────────────────────────────────────────────────────────────────
void motor_setID(uint8_t newID) { g_board_id = newID & 0x07; }
uint8_t motor_getID() { return g_board_id; }

// ─── Motion ──────────────────────────────────────────────────────────────────
void motor_moveTo(uint8_t id, int32_t pos)
{
    if (!id_match(id) || !s_stepper)
        return;
    s_status.target = pos;
    s_status.state = MotorState::MS_MOVING;
    s_stepper->moveTo(pos);
}

void motor_move(uint8_t id, int32_t dist)
{
    if (!id_match(id) || !s_stepper)
        return;
    s_status.target = s_stepper->getCurrentPosition() + dist;
    s_status.state = MotorState::MS_MOVING;
    s_stepper->move(dist);
}

void motor_goHome(uint8_t id)
{
    if (!id_match(id) || !s_stepper)
        return;
    s_status.target = 0;
    s_status.state = MotorState::MS_MOVING;
    s_stepper->moveTo(0);
}

void motor_findHome(uint8_t id)
{
    if (!id_match(id))
        return;
    s_homing_req = true;
}

void motor_stop(uint8_t id)
{
    if (!id_match(id) || !s_stepper)
        return;
    s_stepper->forceStopAndNewPosition(s_stepper->getCurrentPosition());
    s_status.position = s_stepper->getCurrentPosition();
    s_status.target = s_status.position;
    s_status.state = MotorState::MS_IDLE;
}

// ─── Configuration ───────────────────────────────────────────────────────────
void motor_setPosition(uint8_t id, int32_t pos)
{
    if (!id_match(id) || !s_stepper)
        return;
    s_stepper->setCurrentPosition(pos);
    s_status.position = pos;
    s_status.target = pos;
}

void motor_flipDir(uint8_t id)
{
    if (!id_match(id) || !s_stepper)
        return;
    s_status.dirFlipped = !s_status.dirFlipped;
    s_stepper->setDirectionPin(PIN_DIR, !s_status.dirFlipped);
    motor_prefs_save();
}

void motor_setStepSize(uint8_t id, uint16_t ustep)
{
    if (!id_match(id))
        return;
    s_status.microsteps = ustep;
    // microstep config is hardware (MS1/MS2 pins) in standalone mode
    motor_prefs_save();
}

void motor_setCurrent(uint8_t id, uint16_t mA)
{
    if (!id_match(id))
        return;
    s_status.currentMA = mA;
    // current set by VREF hardware in standalone mode
    motor_prefs_save();
}

void motor_setSpeed(uint8_t id, uint32_t hz)
{
    if (!id_match(id))
        return;
    s_status.speedHz = hz ? hz : 1;
    if (s_stepper)
        s_stepper->setSpeedInHz(s_status.speedHz);
    motor_prefs_save();
}

void motor_setAccel(uint8_t id, uint32_t stepsPerSec2)
{
    if (!id_match(id))
        return;
    s_status.accelHz = stepsPerSec2 ? stepsPerSec2 : 100;
    if (s_stepper)
        s_stepper->setAcceleration(s_status.accelHz);
    motor_prefs_save();
}

void motor_enable(uint8_t id, bool en)
{
    if (!id_match(id) || !s_stepper)
        return;
    s_status.enabled = en;
    if (en)
    {
        s_stepper->enableOutputs();
        s_status.state = MotorState::MS_IDLE;
    }
    else
    {
        s_stepper->disableOutputs();
        s_status.state = MotorState::MS_DISABLED;
    }
    motor_prefs_save();
}

// ─── Query ───────────────────────────────────────────────────────────────────
bool motor_getStatus(uint8_t id, MotorStatus &out)
{
    if (!id_match(id))
        return false;
    if (s_stepper)
        s_status.position = s_stepper->getCurrentPosition();
    out = s_status;
    return true;
}
