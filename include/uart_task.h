#pragma once
#include <stdint.h>
#include <stddef.h>

// Initialise the chain UART (Serial1, TX=IO21/IO3, RX=IO20/IO2) and start
// the chain receive task. Call once from setup(), before motor_ctrl_init().
void uart_task_start();

// Write raw bytes to the chain UART (downstream + TMC2209 bus).
void uart_forward(const uint8_t *data, size_t len);
