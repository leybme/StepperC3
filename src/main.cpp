#include <Arduino.h>
#include <board.h>
#include <board_prefs.h>
#include <motor_ctrl.h>
#include <usb_task.h>
#include <uart_task.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>

// ─── Entry point ─────────────────────────────────────────────────────────────
void setup()
{
  Serial.begin(115200); // USB JTAG/CDC (IO18/IO19)
  Serial.println("[StepperC3] boot");
  board_prefs_load(); // restore board preferences from NVS
  motor_ctrl_init();  // init stepper (STEP/DIR standalone)
  uart_task_start();  // init Serial0/Serial1 chain UARTs
  usb_task_start(); // USB CDC command task

  Serial.printf("[StepperC3] ready  board ID=%d  send HELP for commands\r\n",
                g_board_id);
}

void loop()
{
  Serial.printf("ID=%d send HELP for commands\r\n", g_board_id);
  vTaskDelay(pdMS_TO_TICKS(5000));
}