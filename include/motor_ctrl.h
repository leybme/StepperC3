#pragma once
#include <stdint.h>
#include <stddef.h>
#include <stdbool.h>

// ─── Motor state machine ─────────────────────────────────────────────────────
enum class MotorState : uint8_t {
    MS_IDLE     = 0,   // stationary, ready
    MS_MOVING   = 1,   // executing a move
    MS_HOMING   = 2,   // running homing sequence
    MS_STALLED  = 3,   // stall detected (via DIAG pin)
    MS_DISABLED = 4,   // driver disabled
    MS_ERROR    = 5,   // unrecoverable fault
};

// ─── Per-node runtime status ─────────────────────────────────────────────────
struct MotorStatus {
    uint8_t    id;           // node board ID (0-7, mirrors g_board_id)
    int32_t    position;     // current absolute position (steps)
    int32_t    target;       // current target position (steps)
    MotorState state;
    bool       enabled;      // driver output enabled
    bool       dirFlipped;   // direction polarity inverted
    uint16_t   microsteps;   // 1 / 2 / 4 / 8 / 16 / 32 / 64 / 256
    uint16_t   currentMA;    // RMS current (mA)
    uint32_t   speedHz;      // cruise speed (steps/second)
    uint32_t   accelHz;      // acceleration (steps/second²; 0 = use 100)
};

// ─── Lifecycle ───────────────────────────────────────────────────────────────
// Call once from setup() — inits hardware, TMC2209, and starts FreeRTOS task.
void    motor_ctrl_init();

// ─── Identity ────────────────────────────────────────────────────────────────
// Board ID (0-7) is stored in g_board_id (board_prefs.h) and persisted to NVS.
// motor_setID / motor_getID are thin wrappers around g_board_id.
void    motor_setID (uint8_t newID);
uint8_t motor_getID ();

// ─── Motion commands (non-blocking — queued) ─────────────────────────────────
void motor_moveTo   (uint8_t id, int32_t position);   // absolute position
void motor_move     (uint8_t id, int32_t distance);   // relative steps (+/-)
void motor_goHome   (uint8_t id);                     // return to position 0
void motor_findHome (uint8_t id);                     // homing sequence (stall/endstop)
void motor_stop     (uint8_t id);                     // immediate stop, clear queue

// ─── Configuration commands ──────────────────────────────────────────────────
void motor_setPosition (uint8_t id, int32_t pos);        // redefine current pos (no motion)
void motor_flipDir     (uint8_t id);                     // toggle direction polarity
void motor_setStepSize (uint8_t id, uint16_t microsteps);// 1/2/4/8/16/32/64/256
void motor_setCurrent  (uint8_t id, uint16_t mA);        // RMS current in mA
void motor_setSpeed    (uint8_t id, uint32_t stepsPerSec);  // cruise speed in steps/second
void motor_setAccel    (uint8_t id, uint32_t stepsPerSec2); // acceleration steps/s² (0 → 100)
void motor_enable      (uint8_t id, bool en);            // enable / disable driver output

// ─── Query ───────────────────────────────────────────────────────────────────
// Returns false if id does not match this node.
bool motor_getStatus (uint8_t id, MotorStatus &out);

// ─── Chain helper ────────────────────────────────────────────────────────────
// Prefer including uart_task.h and calling uart_forward() directly.
// This shim is kept for motor_ctrl.cpp internal use only.
