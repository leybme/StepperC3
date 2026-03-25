# StepperC3 — ESP32-C3 Daisy-Chain Stepper Controller

**Author:** Nguyen Le Y  
**Assisted by:** Claude Sonnet (Anthropic) & GitHub Copilot (Microsoft)  
**Hardware:** [StepperControllerC3 Pro on OSHWLab](https://oshwlab.com/nguyenleypro/steppercontrollerc3_pro)

---

A firmware for the **StepperControllerC3 Pro** custom board (ESP32-C3-MINI-1-N4) that drives a **TMC2209** stepper motor in STEP/DIR
standalone mode while participating in a daisy-chain command bus of up to 8 nodes.
Each node has a unique board ID (0–7). Commands are routed by ID; a non-matching command
is automatically forwarded downstream.

---

## Hardware

### BOM (per node)
| Part | Value / Part No. |
|---|---|
| Main board | StepperControllerC3 Pro ([OSHWLab](https://oshwlab.com/nguyenleypro/steppercontrollerc3_pro)) |
| MCU | ESP32-C3-MINI-1-N4 (on-board) |
| Stepper driver | **TMC2209 V3.1** — Fysetc SilentStepStick |
| Sense resistors | 0.11 Ω (on Fysetc V3.1 board, see `TMC_R_SENSE`) |
| End switch | Normally-open micro switch, wired IO5 → GND |
| Power input | USB-PD negotiated supply on **Board 0** only; downstream boards powered via daisy-chain power rail |

### Daisy-Chain Wiring
```
USB-PD (5V–20V) ──► Board 0 ──► Board 1 UART_IN (IO3)
     │               │ UART_OUT    Board 1 UART_OUT (IO21) ──► Board 2 UART_IN (IO3)
     │               │ (IO21)                               Board 2 UART_OUT (IO21) ──► …
     │               │
     └── USB/PC ─────┘ (USB CDC — commands & monitoring)
```
> **Power note:** Only **Board 0** needs USB-PD and a USB/PC connection.
> Power and UART signals are both carried downstream through the daisy-chain — downstream boards
> do not need their own USB-PD ports.
> 20 V is recommended for adequate motor torque headroom; 5 V also works but limits maximum current/speed.

Commands sent to Board 0 propagate downstream automatically.
UART is **SERIAL_8E1** (even parity) at **115200 baud** for noise rejection.

---

## Pin Assignments (`include/board.h`)

| Define | IO | Function |
|---|---|---|
| `PIN_DIR` | IO0 | TMC2209 DIR |
| `PIN_STEP` | IO1 | TMC2209 STEP |
| `PIN_STEP_EN` | IO4 | TMC2209 EN (active LOW) |
| `PIN_DIAG` | IO5 | End-switch input during homing; RX-blink output otherwise |
| `TMC_PIN_TX` | IO6 | TMC2209 PDN_UART TX (boot-time config only) |
| `TMC_PIN_RX` | IO7 | TMC2209 PDN_UART RX (reserved, unused) |
| `PIN_UART_IN_RX` | IO3 | Chain UART IN — from upstream |
| `PIN_UART_IN_TX` | IO2 | Chain UART IN — ACK to upstream |
| `PIN_UART_OUT_TX` | IO21 | Chain UART OUT — to downstream |
| `PIN_UART_OUT_RX` | IO20 | Chain UART OUT — RX (unused) |
| `PIN_I2C_SDA` | IO10 | I2C SDA (reserved) |
| `PIN_I2C_SCL` | IO8 | I2C SCL (reserved) |
| `PIN_BOOT` | IO9 | BOOT button (active LOW) |

---

## Hard Configuration Variables

Edit these in `include/board.h` before flashing:

| Variable | Default | Description |
|---|---|---|
| `TMC_R_SENSE` | `0.11f` | Current-sense resistor value (Ω). Match your driver hardware. |

Edit these in `src/motor_ctrl.cpp` (`s_status` initialiser) for factory defaults
(overridden by NVS after first `SETCUR` / `SETSTEP` etc.):

| Field | Default | Description |
|---|---|---|
| `microsteps` | `16` | Initial microstep setting |
| `currentMA` | `600` | Initial RMS current (mA) |
| `speedHz` | `1000` | Initial cruise speed (steps/s) |
| `accelHz` | `1000` | Initial acceleration (steps/s²) |
| `dirFlipped` | `false` | Initial direction polarity |
| `enabled` | `true` | Driver enabled at boot |

Edit in `src/uart_task.cpp`:

| Define | Default | Description |
|---|---|---|
| `BAUD_CHAIN` | `115200` | Chain UART baud rate |
| `UART_CONFIG` | `SERIAL_8E1` | UART frame format — even parity |

---

## Persistent Storage (NVS)

Two NVS namespaces are used:

| Namespace | Key | Type | Description |
|---|---|---|---|
| `stepper` | `board_id` | `uint8` | Board ID (0–7) |
| `motor` | `dirFlip` | `bool` | Direction polarity |
| `motor` | `microsteps` | `uint16` | Microstep resolution |
| `motor` | `currentMA` | `uint16` | RMS current (mA) |
| `motor` | `speedHz` | `uint32` | Cruise speed (steps/s) |
| `motor` | `accelHz` | `uint32` | Acceleration (steps/s²) |
| `motor` | `enabled` | `bool` | Driver enable state |

NVS persists across reboots and power cycles. Factory defaults are used
only when a key is absent (i.e. on a fresh chip or after `nvs_flash_erase`).

---

## Task Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  setup()                                                        │
│  board_prefs_load() → motor_ctrl_init() → uart_task_start()    │
│                       → usb_task_start()                       │
└──────────────┬──────────────────────────────────────────────────┘
               │ creates FreeRTOS tasks
               ▼
┌──────────────────────┐   ┌──────────────────────┐   ┌────────────────────────┐
│   usbTask  (pri 2)   │   │   uartTask (pri 2)   │   │  motorTask  (pri 1)   │
│                      │   │                      │   │                        │
│  Serial (USB CDC)    │   │  Serial0 IN (IO3)    │   │  FastAccelStepper ISR  │
│  line buffer         │   │  line buffer         │   │  homing loop           │
│  process_cmd()  ─────┼───► process_cmd()        │   │  state tracking        │
│                      │   │  uart_forward()      │   │                        │
└──────────────────────┘   └──────────────────────┘   └────────────────────────┘
                                    │
                             Serial1 OUT (IO21)
                             guarded by g_serial1_mutex
                                    │
                           ┌────────┴─────────┐
                           │  Downstream node  │
                           │  or TMC2209 UART  │
                           │  (boot / reconfig)│
                           └───────────────────┘
```

### Task Summary

| Task | Stack | Priority | Source |
|---|---|---|---|
| `usbTask` | 4096 B | 2 | `src/usb_task.cpp` |
| `uartTask` | 4096 B | 2 | `src/uart_task.cpp` |
| `motorTask` | 2048 B | 1 | `src/motor_ctrl.cpp` |

### Key Functions

| Function | File | Description |
|---|---|---|
| `motor_ctrl_init()` | `motor_ctrl.cpp` | Load NVS, configure TMC2209 via UART, init FastAccelStepper, start motorTask |
| `uart_task_start()` | `uart_task.cpp` | Init Serial0/Serial1, create g_serial1_mutex, start uartTask |
| `usb_task_start()` | `usb_task.cpp` | Start usbTask |
| `process_cmd(char*)` | `usb_task.cpp` | Parse and dispatch one ASCII command line |
| `uart_forward(data,len)` | `uart_task.cpp` | Write bytes downstream via Serial1 (mutex-protected) |
| `tmc_reconfigure()` | `motor_ctrl.cpp` | Borrow Serial1, push current/microstep/dir to TMC2209 UART, restore |
| `board_prefs_load()` | `board_prefs.cpp` | Read board ID from NVS into `g_board_id` |
| `board_prefs_save_id(id)` | `board_prefs.cpp` | Persist board ID to NVS |
| `motor_prefs_save()` | `motor_ctrl.cpp` (static) | Persist all motor config to NVS namespace `motor` |
| `apply_speed_accel()` | `motor_ctrl.cpp` (static) | Push speedHz/accelHz to FastAccelStepper |

---

## Command Protocol

All commands are **ASCII, newline-terminated** (`\n` or `\r\n`).  
Send via USB CDC serial terminal at **115200 baud**.

### Format

```
<id> <COMMAND> [arg]
```

- `id` = target board ID (0–7)
- If `id` matches this board → **execute locally**
- If `id` does not match → **forward raw line downstream**

### Special Commands (no ID prefix)

| Command | Syntax | Description |
|---|---|---|
| `HELP` | `HELP` | Print command reference to USB serial |
| `SETID` | `SETID <n>` | Set this board's ID to `n`, persist to NVS, then forward `SETID n+1` downstream — auto-numbers the whole chain |

### Motion Commands

| Command | Syntax | Description |
|---|---|---|
| `MOVETO` | `<id> MOVETO <pos>` | Move to absolute step position (non-blocking) |
| `MOVE` | `<id> MOVE <steps>` | Move relative steps, `+` or `−` |
| `GOHOME` | `<id> GOHOME` | Return to position 0 |
| `FINDHOME` | `<id> FINDHOME` | Run homing: creep at 200 Hz until IO5 (end switch) goes LOW, zero position |
| `STOP` | `<id> STOP` | Immediate stop, clear motion |

### Configuration Commands

| Command | Syntax | Description |
|---|---|---|
| `SETPOS` | `<id> SETPOS <pos>` | Redefine current position counter (no motion) |
| `FLIPDIR` | `<id> FLIPDIR` | Toggle direction polarity, update TMC2209 `shaft` register live |
| `SETSTEP` | `<id> SETSTEP <n>` | Set microsteps: 1/2/4/8/16/32/64/256 — updates TMC2209 live |
| `SETCUR` | `<id> SETCUR <mA>` | Set RMS current — updates TMC2209 live |
| `SETSPD` | `<id> SETSPD <hz>` | Set cruise speed in **steps/second** |
| `SETACCEL` | `<id> SETACCEL <n>` | Set acceleration in **steps/s²** (0 → uses 100) |
| `ENABLE` | `<id> ENABLE` | Enable driver output |
| `DISABLE` | `<id> DISABLE` | Disable driver output |

### Query Commands

| Command | Syntax | Description |
|---|---|---|
| `STATUS` | `<id> STATUS` | Print full status line |

### Responses

```
OK <COMMAND> <id>
STATUS <id> pos=<n> tgt=<n> state=<s> en=<0/1> flip=<0/1> step=<n> cur=<n>mA spd=<n>hz accel=<n>
ERR <reason>
```

States: `IDLE` `MOVING` `HOMING` `STALLED` `DISABLED` `ERROR`

---

## Instruction for Use

### First-Time Setup (single board)

1. Flash firmware via USB (`pio run --target upload`)
2. Open serial monitor at 115200 baud
3. Send `SETID 0` — assigns this board ID 0, persists to NVS
4. Send `0 SETCUR 600` — set RMS current to 600 mA (adjust for your motor)
5. Send `0 SETSTEP 16` — set 16 microsteps
6. Send `0 SETSPD 2000` — set cruise speed 2000 steps/s
7. Send `0 SETACCEL 1000` — set acceleration 1000 steps/s²
8. Send `0 ENABLE` — enable driver
9. Send `0 MOVE 1600` — test move (1600 steps = 1 rev at 16 ustep)

### Auto-Numbering a Full Chain

Connect all boards in series (UART_OUT of N → UART_IN of N+1). Power all, then
send **once** to the first board:

```
SETID 0
```

Board 0 sets itself to ID 0, then forwards `SETID 1` downstream.
Board 1 sets itself to ID 1, forwards `SETID 2`, and so on up to ID 7.
All IDs are saved to NVS — IDs survive a power cycle.

### Sending to a Specific Board

```
3 MOVETO 5000     ← board 3 moves to absolute position 5000
3 STATUS          ← query board 3
5 SETCUR 800      ← set 800 mA on board 5
```

### Homing

Wire a normally-open end switch between **IO5** and **GND**.

```
0 FINDHOME
```

The motor creeps at 200 Hz toward negative infinity. When IO5 is pulled LOW
(switch closed), the motor stops and position is zeroed. If no switch triggers
within 2,000,000 steps the move completes anyway.

---

## Build & Flash

```sh
# Build
pio run

# Flash (adjust port)
pio run --target upload --upload-port COM9

# Monitor
pio device monitor --port COM9 --baud 115200
```

### Dependencies (`platformio.ini`)

| Library | Source |
|---|---|
| `FastAccelStepper` | `https://github.com/gin66/FastAccelStepper.git` (git HEAD — required for ESP-IDF 5.x RMT v2 API) |
| `TMCStepper` | `teemuatlut/TMCStepper @ ^0.7.3` |

---

## Notes

- **TMC2209 UART is TX-only at boot.** `SETSTEP`, `SETCUR`, and `FLIPDIR` push live
  updates to the driver by temporarily borrowing `Serial1` under `g_serial1_mutex`.
  Register readback is not implemented.

- **`PIN_DIAG` (IO5) is dual-purpose.** During normal operation it is driven as an
  OUTPUT blink indicator by `uartTask`. During `FINDHOME` it is switched to
  `INPUT_PULLUP`, then restored to OUTPUT when homing completes.

- **SERIAL_8E1** (even parity) is used on the chain UARTs. A single flipped bit
  is rejected in hardware, reducing spurious command execution in electrically noisy
  environments.

- **`Serial1` is shared** between chain UART OUT and TMC2209 UART config.
  `g_serial1_mutex` (FreeRTOS mutex) prevents races between `uart_forward()` and
  `tmc_reconfigure()`.

- **Position resets to 0 on every reboot.** Only configuration (speed, current,
  microsteps, direction, enable) is persisted. For absolute positioning across
  power cycles, run `FINDHOME` at startup before any motion.

- **Maximum chain length** is 8 boards (IDs 0–7). `SETID 7` does not forward
  further downstream.
