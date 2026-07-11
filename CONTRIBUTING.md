# Guía de contribución

¡Gracias por tu interés en mejorar **CoreTempLinux**! Este documento resume cómo
colaborar de forma efectiva.

## Antes de empezar

- El proyecto es **exclusivamente para Linux**: depende de `/sys` y `/proc`.
- La **interfaz y los comentarios del código están en español**. Mantén esa
  convención en tus cambios.
- Necesitas **.NET 10 SDK** y **GTK 4** instalados (ver [README](README.md)).

## Flujo de trabajo

1. Haz un *fork* del repositorio y crea una rama descriptiva:
   ```bash
   git checkout -b fix/lectura-hwmon
   ```
2. Realiza tus cambios en commits pequeños y con mensajes claros.
3. Compila y prueba localmente antes de enviar:
   ```bash
   dotnet build
   dotnet run
   ```
4. Abre un *pull request* contra la rama `main` describiendo qué cambia y por qué.

## Estilo de código

- Respeta el `.editorconfig` incluido (indentación de 4 espacios, `namespace`
  con ámbito de archivo, `nullable` habilitado).
- **Los lectores de sensores nunca deben lanzar excepciones**: cualquier fallo de
  lectura en `/sys` o `/proc` debe degradar de forma segura (lista vacía, `NaN` o
  `null`), porque los sensores disponibles varían según el hardware.
- Los callbacks persistentes del temporizador GLib se guardan en un campo para
  evitar que el GC los recoja.
- Los valores que van a *markup* de GTK pasan por `Escape()`; la conversión de
  unidades crudas del kernel a unidades humanas se hace en `HwmonReader.Add`, no
  en la UI.

## Reportar errores

Usa las plantillas de *issues*. Incluye:

- Tu distribución y versión de kernel (`uname -a`).
- Modelo de CPU y, si es posible, la salida de `sensors` o el contenido relevante
  de `/sys/class/hwmon`.
- Pasos para reproducir y comportamiento esperado vs. observado.

## Licencia de las contribuciones

Al enviar código aceptas que se distribuya bajo la licencia **GPLv3** del
proyecto.
