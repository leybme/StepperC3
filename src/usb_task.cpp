#include "usb_task.h"
#include "motor_ctrl.h"
#include "uart_task.h"
#include <Arduino.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <string.h>
#include <stdlib.h>

// ─── USB CDC Command Protocol ─────────────────────────────────────────────────
//
//  Format (ASCII, newline-terminated):
//    <COMMAND> [motorID] [arg1] [arg2]
//
//  COMMAND     ARGS              DESCRIPTION
//  ──────────  ────────────────  ──────────────────────────────────────────────
//  SETID       <id>              Set this node's motor ID (0-9)
//  MOVETO      <id> <pos>        Absolute move to step position
//  MOVE        <id> <steps>      Relative move (+/− steps)
//  GOHOME      <id>              Return to position 0
//  FINDHOME    <id>              Run homing sequence (stall detection)
//  SETPOS      <id> <pos>        Redefine current position (no motion)
//  FLIPDIR     <id>              Toggle direction polarity
//  SETSTEP     <id> <ustep>      Set microsteps (1/2/4/8/16/32/64/256)
//  SETCUR      <id> <mA>         Set RMS current (mA)
//  SETSPD      <id> <us>         Set step half-period µs  (lower = faster)
//  SETACCEL    <id> <steps>      Set acceleration ramp length (0 = off)
//  ENABLE      <id>              Enable stepper driver output
//  DISABLE     <id>              Disable stepper driver output
//  STOP        <id>              Emergency stop, clear queue
//  STATUS      <id>              Query full status
//  HELP                          Print command reference
//
//  Responses:
//    OK <COMMAND> <id>
//    STATUS <id> pos=<n> tgt=<n> state=<s> en=<0/1> flip=<0/1>
//           step=<n> cur=<n>mA spd=<n>us accel=<n>
//    ERR <reason>
// ─────────────────────────────────────────────────────────────────────────────

static void send_ok(const char *cmd, int id = -1) {
    if (id >= 0) Serial.printf("OK %s %d\r\n", cmd, id);
    else         Serial.printf("OK %s\r\n", cmd);
}

static void send_err(const char *reason) {
    Serial.printf("ERR %s\r\n", reason);
}

static const char *state_str(MotorState s) {
    switch (s) {
        case MotorState::MS_IDLE:     return "IDLE";
        case MotorState::MS_MOVING:   return "MOVING";
        case MotorState::MS_HOMING:   return "HOMING";
        case MotorState::MS_STALLED:  return "STALLED";
        case MotorState::MS_DISABLED: return "DISABLED";
        case MotorState::MS_ERROR:    return "ERROR";
        default:                   return "?";
    }
}

static void process_cmd(char *line) {
    char *tok[8] = {};
    int   n      = 0;
    char *p      = strtok(line, " \t\r\n");
    while (p && n < 8) { tok[n++] = p; p = strtok(nullptr, " \t\r\n"); }
    if (n == 0) return;

    const char *cmd = tok[0];

    // ── HELP ──────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "HELP") == 0) {
        Serial.print(
            "\r\nCommands:\r\n"
            "  SETID    <id>            Set this node's motor ID (0-9)\r\n"
            "  MOVETO   <id> <pos>      Absolute move (steps)\r\n"
            "  MOVE     <id> <steps>    Relative move (+/-)\r\n"
            "  GOHOME   <id>            Return to position 0\r\n"
            "  FINDHOME <id>            Run homing sequence\r\n"
            "  SETPOS   <id> <pos>      Redefine current position (no motion)\r\n"
            "  FLIPDIR  <id>            Toggle direction polarity\r\n"
            "  SETSTEP  <id> <ustep>    Microsteps: 1/2/4/8/16/32/64/256\r\n"
            "  SETCUR   <id> <mA>       RMS current (mA)\r\n"
            "  SETSPD   <id> <us>       Step half-period µs (lower = faster)\r\n"
            "  SETACCEL <id> <steps>    Accel ramp steps (0 = off)\r\n"
            "  ENABLE   <id>            Enable driver\r\n"
            "  DISABLE  <id>            Disable driver\r\n"
            "  STOP     <id>            Emergency stop\r\n"
            "  STATUS   <id>            Query status\r\n"
            "  HELP                     This message\r\n"
        );
        return;
    }

    // ── SETID — no motorID argument; applies to this node ────────────────────
    if (strcasecmp(cmd, "SETID") == 0) {
        if (n < 2) { send_err("SETID needs <id>"); return; }
        uint8_t id = (uint8_t)atoi(tok[1]);
        if (id > 9) { send_err("id must be 0-9"); return; }
        motor_setID(id);
        send_ok("SETID", id);
        return;
    }

    // ── All remaining commands require a motorID argument ─────────────────────
    if (n < 2) { send_err("missing motorID"); return; }
    uint8_t id = (uint8_t)atoi(tok[1]);

    // ── STATUS ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "STATUS") == 0) {
        MotorStatus st;
        if (!motor_getStatus(id, st)) { send_err("wrong id"); return; }
        Serial.printf(
            "STATUS %d pos=%ld tgt=%ld state=%s en=%d flip=%d"
            " step=%u cur=%umA spd=%luus accel=%lu\r\n",
            st.id, (long)st.position, (long)st.target, state_str(st.state),
            (int)st.enabled, (int)st.dirFlipped,
            st.microsteps, st.currentMA,
            (unsigned long)st.speedUs, (unsigned long)st.accelSteps);
        return;
    }

    // ── MOVETO ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "MOVETO") == 0) {
        if (n < 3) { send_err("MOVETO needs <id> <pos>"); return; }
        motor_moveTo(id, atol(tok[2]));
        send_ok("MOVETO", id); return;
    }

    // ── MOVE ──────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "MOVE") == 0) {
        if (n < 3) { send_err("MOVE needs <id> <steps>"); return; }
        motor_move(id, atol(tok[2]));
        send_ok("MOVE", id); return;
    }

    // ── GOHOME ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "GOHOME") == 0) {
        motor_goHome(id); send_ok("GOHOME", id); return;
    }

    // ── FINDHOME ──────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "FINDHOME") == 0) {
        motor_findHome(id); send_ok("FINDHOME", id); return;
    }

    // ── STOP ──────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "STOP") == 0) {
        motor_stop(id); send_ok("STOP", id); return;
    }

    // ── SETPOS ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "SETPOS") == 0) {
        if (n < 3) { send_err("SETPOS needs <id> <pos>"); return; }
        motor_setPosition(id, atol(tok[2]));
        send_ok("SETPOS", id); return;
    }

    // ── FLIPDIR ───────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "FLIPDIR") == 0) {
        motor_flipDir(id); send_ok("FLIPDIR", id); return;
    }

    // ── SETSTEP ───────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "SETSTEP") == 0) {
        if (n < 3) { send_err("SETSTEP needs <id> <ustep>"); return; }
        motor_setStepSize(id, (uint16_t)atoi(tok[2]));
        send_ok("SETSTEP", id); return;
    }

    // ── SETCUR ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "SETCUR") == 0) {
        if (n < 3) { send_err("SETCUR needs <id> <mA>"); return; }
        motor_setCurrent(id, (uint16_t)atoi(tok[2]));
        send_ok("SETCUR", id); return;
    }

    // ── SETSPD ────────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "SETSPD") == 0) {
        if (n < 3) { send_err("SETSPD needs <id> <us>"); return; }
        motor_setSpeed(id, (uint32_t)atol(tok[2]));
        send_ok("SETSPD", id); return;
    }

    // ── SETACCEL ──────────────────────────────────────────────────────────────
    if (strcasecmp(cmd, "SETACCEL") == 0) {
        if (n < 3) { send_err("SETACCEL needs <id> <steps>"); return; }
        motor_setAccel(id, (uint32_t)atol(tok[2]));
        send_ok("SETACCEL", id); return;
    }

    // ── ENABLE / DISABLE ──────────────────────────────────────────────────────
    if (strcasecmp(cmd, "ENABLE") == 0)  { motor_enable(id, true);  send_ok("ENABLE",  id); return; }
    if (strcasecmp(cmd, "DISABLE") == 0) { motor_enable(id, false); send_ok("DISABLE", id); return; }

    send_err("unknown command — send HELP");
}

// ─── FreeRTOS USB task ───────────────────────────────────────────────────────
static void usbTask(void *) {
    static char buf[256];
    uint16_t    idx = 0;

    for (;;) {
        while (Serial.available()) {
            char c = (char)Serial.read();
            if (idx < sizeof(buf) - 1) buf[idx++] = c;
            if (c == '\n') {
                buf[idx] = '\0';
                // Forward a copy of the raw line downstream
                uart_forward((const uint8_t *)buf, idx);
                process_cmd(buf);
                idx = 0;
            }
        }
        vTaskDelay(pdMS_TO_TICKS(10));
    }
}

void usb_task_start() {
    xTaskCreate(usbTask, "usb", 4096, nullptr, 2, nullptr);
}
