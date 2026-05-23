# UltraStar Simplificado

Ejecutable:

```text
C:\Users\Jordi\OneDrive\Documentos\New project\dist\UltraStarSimplificado.exe
```

## Que hace

- Abre directo en fullscreen.
- Muestra solo caratulas de canciones UltraStar.
- Click en una caratula para cantar.
- Reproduce el video/audio con letras tipo karaoke.
- Incluye control de tono.
- Incluye modo karaoke para reducir voz central del video.
- No tiene microfonos, estadisticas ni configuracion avanzada.

## Donde busca canciones

Busca automaticamente canciones `.txt` UltraStar en estas carpetas:

```text
Carpeta del ejecutable\songs
Documentos\UltraStar Songs
C:\PROYECTOS JORDI\CANCIONES ULTRASTAR
C:\Program Files (x86)\UltraStar WorldParty\songs
```

Si no encuentra canciones, pide elegir una carpeta.

## Controles

- Click: reproducir cancion.
- Espacio: play/pausa.
- Escape: volver al selector; si ya estas en el selector, sale.
- F11: activar/desactivar fullscreen.
- Tono: sube o baja semitonos.
- Karaoke: reduce progresivamente la voz central.

## Requisito

`mpv.exe` debe estar junto al ejecutable o en la carpeta `dist`.
