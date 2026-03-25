#include <Arduino.h>
#include <board.h>
#include <motor_ctrl.h>
#include <usb_task.h>
#include <uart_task.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>

// ─── Entry point ─────────────────────────────────────────────────────────────
void setup() {
    Serial.begin(115200);   // USB JTAG/CDC (IO18/IO19)
    for (uint32_t t = millis(); !Serial && (millis() - t) < 10000; ) delay(10);
    Serial.println("[StepperC3] boot");

    uart_task_start();     // init Serial1 (chain UART: IO20/IO21 = IO2/IO3)
    motor_ctrl_init();     // init TMC2209 over Serial1 (must be after uart_task_start)
    usb_task_start();      // USB CDC command task

    Serial.printf("[StepperC3] ready  motor ID=%d  send HELP for commands\r\n",
                  motor_getID());
}

void loop() {
    vTaskDelay(pdMS_TO_TICKS(1000));
}