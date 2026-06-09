# ⚡ XAMPP SQL Importer

A lightweight Windows utility for importing `.sql` files into a local XAMPP MySQL database — fast, via the command line under the hood, with a clean UI on top.

Built because phpMyAdmin's native import is painfully slow for large SQL files. This tool runs the same `mysql.exe` command you'd type in a terminal, but wrapped in a simple point-and-click interface.

---

## Features

- **Auto-detects mysql.exe** across common XAMPP install locations and drive letters
- **Remembers your setup** — saves the verified mysql.exe path to `config.json` so you don't configure it twice
- **Live database dropdown** — queries MySQL directly for your databases on startup, no manual typing
- **Real-time output log** — see exactly what MySQL is doing while the import runs
- **Import time** — shows how long the import took when it completes

---

## Requirements

- Windows 10 or 11
- [XAMPP](https://www.apachefriends.org/) installed with MySQL running
- .NET 8 SDK *(only needed to build — the final `.exe` is self-contained)*

---

## Getting Started

### 1. Build the app

Run `build.bat` by double-clicking it.

- If .NET 8 SDK is not installed, the script will **download and install it automatically**
- The finished executable will appear at `.\publish\SqlImporter.exe`
- That single `.exe` file is all you need — copy it anywhere

### 2. Run the app

Double-click `SqlImporter.exe`. On first launch it will:

1. Scan common locations to find `mysql.exe`
2. Connect to your running XAMPP MySQL instance
3. Populate the database dropdown with your databases

> Make sure XAMPP's MySQL module is **started** (green in the XAMPP Control Panel) before launching.

---

## Usage

| Field | Description |
|---|---|
| **MySQL Executable** | Path to `mysql.exe`. Auto-detected on startup. Use Browse… to set manually. |
| **Username** | MySQL username. Default is `root`. |
| **Password** | MySQL password. Leave blank if you haven't set one (default XAMPP setup). |
| **Database** | Dropdown of available databases. Click **↻ Refresh** to reload. |
| **SQL File** | The `.sql` file to import. Click **Browse…** to select. |

Once everything is filled in, click **▶ Run Import**. The output log will show live feedback and a green ✔ (or red ✘) when done.

---

## Config

The app saves a `config.json` file in the same folder as the `.exe`:

```json
{
  "MysqlPath": "C:\\Development\\XAMPP\\mysql\\bin\\mysql.exe"
}
```

This is written automatically the first time a working `mysql.exe` is found — either by auto-detection or manual Browse. You can delete it to reset back to auto-detection.

---

## Auto-detection Paths

On startup, the app scans the following locations across drives C through G:

```
X:\xampp\mysql\bin\mysql.exe
X:\xampp\bin\mysql.exe
X:\Development\XAMPP\mysql\bin\mysql.exe
X:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe
X:\Program Files\MySQL\MySQL Server 5.7\bin\mysql.exe
X:\Program Files (x86)\MySQL\MySQL Server 8.0\bin\mysql.exe
X:\Program Files\MariaDB 10.11\bin\mysql.exe
X:\Program Files\MariaDB 10.6\bin\mysql.exe
```

It also checks the `XAMPP_HOME` environment variable if set. If none of these match your setup, use the **Browse…** button next to the MySQL Executable field — the path will be saved automatically if it works.

---

## Troubleshooting

**Dropdown is empty / "Is XAMPP running?"**
→ Open the XAMPP Control Panel and make sure the MySQL module shows a green status. Then click **↻ Refresh**.

**"mysql.exe not found"**
→ Use the Browse… button to manually locate `mysql.exe`. It lives inside your XAMPP install folder under `mysql\bin\mysql.exe`.

**Import fails with exit code 1**
→ Check the output log for the MySQL error message. Common causes: wrong database selected, SQL file references a database that doesn't exist, or a syntax error in the file.

**Access denied error**
→ Your username or password is incorrect. The default XAMPP setup uses `root` with no password.

---

## Built With

- C# / .NET 8
- Windows Forms
- No third-party dependencies
