# LongYin Money Probe

This is the first gameplay-logic probe for the IL2CPP build.

Goal:
- prove that a Harmony patch can hook a real gameplay method,
- change behavior safely,
- and give a tiny visible result without destabilizing saves or combat.

Current hook:
- `HeroData.ChangeMoney(int num, bool showInfo)`

Current behavior:
- when the player gains positive money, the mod adds a small extra bonus,
- default bonus is `+1`,
- `F7` toggles the prototype on or off,
- `F8` logs the current player money into the BepInEx log.

## Files

- `LongYinMoneyProbe.cs`: plugin source
- `build-money-probe.ps1`: builds the DLL and stages it into `_codex_disabled_loader/BepInEx/plugins`
- `enable-staged-loader.ps1`: copies the staged BepInEx loader back into the live game root and enables Doorstop

## Safe Test Flow

1. Build the plugin with `build-money-probe.ps1`.
2. Enable the staged loader with `enable-staged-loader.ps1`.
3. Launch the game.
4. Load a disposable save.
5. Trigger any small positive money gain.
6. Confirm the gain is larger than expected by the configured bonus.
7. Press `F8` if you want the current money value logged.

## Rollback

- Disable the loader by restoring from the `_codex_live_backup_*` folder created by `enable-staged-loader.ps1`.
- Or set `enabled = false` in the live `doorstop_config.ini`.
- Or remove the plugin DLL from `BepInEx/plugins`.

## Notes

- This probe is intentionally tiny.
- If this hook works cleanly, the next prototype should be another narrow logic seam such as move range, EXP gain, or battle damage.
