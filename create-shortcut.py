"""Create a Windows shortcut. Install pylnk3 to run this packaging helper."""
from pathlib import Path
import sys
import pylnk3

root = Path(__file__).resolve().parent
destination = Path(sys.argv[1])
executable = Path(sys.argv[2]) if len(sys.argv) > 2 else root / 'dist' / 'EnglishCompanion.exe'
icon = Path(sys.argv[3]) if len(sys.argv)>3 else executable if len(sys.argv) > 2 else root / 'assets' / 'companion.ico'
if destination.exists():
    raise SystemExit('Shortcut already exists; preserve it and choose another name.')
pylnk3.for_file(str(executable),
               lnk_name=str(destination), description='语伴 · 打开模型配置',
               icon_file=str(icon), icon_index=0,
               work_dir=str(executable.parent), window_mode=pylnk3.WINDOW_NORMAL)
shortcut = pylnk3.Lnk(str(destination))
assert not shortcut.arguments
assert shortcut.work_dir == str(executable.parent)
assert shortcut.icon == str(icon)
print('Shortcut created and metadata verified.')
