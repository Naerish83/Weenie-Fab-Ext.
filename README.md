# WeenieFab — Wave 6

### What’s new
- **DID Value Picker**: searchable, type-aware input (decimal or 0xHEX) with DAT presence indicator
- **DAT-awareness**: prompts for Portal.dat once; status dot = green (exists), orange (not in DAT)
- **DB probe**: auto-falls back to offline mode if MySQL unreachable (UI still works)
- **No-crash UX**: designer-safe and guarded dialogs

### Setup
1) Install .NET + VS, open solution.
2) Create a local `dbconfig.local.json` (ignored by git):
   ```json
   { "connectionString": "server=127.0.0.1;port=3308;database=ace_world;user id=root;password=root;SslMode=None;AllowPublicKeyRetrieval=True;Default Command Timeout=5;Connection Timeout=4;" }
