#!/usr/bin/env bash
#
# Desinstala CoreTempLinux del prefijo indicado (por defecto ~/.local).
#
# Uso:   ./uninstall.sh
#        sudo PREFIX=/usr ./uninstall.sh
#
set -euo pipefail

APPID="org.coretemplinux.App"
PREFIX="${PREFIX:-$HOME/.local}"
LIBDIR="$PREFIX/lib/coretemplinux"
ICONDIR="$PREFIX/share/icons/hicolor"
APPDIR="$PREFIX/share/applications"

echo ">> Eliminando binarios de $LIBDIR"
rm -rf "$LIBDIR"

echo ">> Eliminando iconos"
for size in 16 32 48 64 128 256 512; do
    rm -f "$ICONDIR/${size}x${size}/apps/${APPID}.png"
done

echo ">> Eliminando lanzador"
rm -f "$APPDIR/${APPID}.desktop"

echo ">> Actualizando cachés…"
gtk4-update-icon-cache -q -t -f "$ICONDIR" 2>/dev/null \
    || gtk-update-icon-cache -q -t -f "$ICONDIR" 2>/dev/null || true
update-desktop-database "$APPDIR" 2>/dev/null || true

echo "✔ Desinstalado."
