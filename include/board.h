#pragma once

// ─── ESP32-C3-MINI-1-N4 pin assignments ─────────────────────────────────────

// Stepper (TMC2209 — STEP/DIR standalone, no UART)
#define PIN_DIR       0   // IO0
#define PIN_STEP      1   // IO1
#define PIN_STEP_EN   4   // IO4  — LOW = enabled
#define PIN_DIAG      5   // IO5  — DIAG output from TMC2209
// TMC2209
#define TMC_PIN_TX        6 
#define TMC_PIN_RX        7  

// UART IN: from upstream node (Serial0 / UART_NUM_0)
#define PIN_UART_IN_RX   3   // IO3 — RX from upstream
#define PIN_UART_IN_TX   2   // IO2 — TX ACK back to upstream

// UART OUT: to downstream node (Serial1 / UART_NUM_1)
#define PIN_UART_OUT_TX 21   // IO21 — TX to downstream
#define PIN_UART_OUT_RX 20   // IO20 — RX (unused, required by Serial1.begin)

// I2C
#define PIN_I2C_SDA   10  // IO10
#define PIN_I2C_SCL   8   // IO8

// Misc
#define PIN_BOOT      9   // IO9  — BOOT button (active LOW)

// USB (handled by hardware, listed for reference only)
// #define PIN_USB_DM   18  // IO18 / ESP_D-
// #define PIN_USB_DP   19  // IO19 / ESP_D+
