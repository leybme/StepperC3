#pragma once

// Start the USB CDC command task. Call once from setup().
void usb_task_start();

// Parse and execute (or forward) one command line.
// The line is mutated by strtok — pass a writable buffer.
void process_cmd(char *line);
