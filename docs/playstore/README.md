# TerraNet en Google Play

Esta rama (`playstore`) es la versión de Android para Google Play. `main` no cambia y sigue siendo la versión de la web y de GitHub.

## Qué cambia respecto a main

| Cambio | Por qué |
| --- | --- |
| Sin actualización propia en Android (`SupportsSelfUpdate => false`, botón de actualizar oculto, no se consulta terra.hacks.win) | Play prohíbe que una app se actualice por fuera de Play |
| Sin permiso `REQUEST_INSTALL_PACKAGES` ni `FileProvider` | Solo servían para instalar la APK descargada |
| El servicio declara `<property android:name="android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE">` | Obligatorio para servicios en primer plano de tipo `specialUse` |
| `tools/release/playstore.sh` genera un `.aab` firmado | Play solo acepta Android App Bundle para apps nuevas |
| Gráficos y capturas en `docs/playstore/graphics` y `docs/playstore/screenshots` | Ficha de la tienda |

La app en PC (`TerraNet.exe`) no se toca en esta rama.

## Compilar el bundle

```sh
REUSE_PACKS=1 bash tools/release/playstore.sh 0.1.5
```

Sale en `artifacts/playstore/0.1.5/TerraNet-0.1.5.aab`, firmado con `secrets/terranet.keystore` (alias `terranet`). Cada subida a Play necesita un `versionCode` mayor que la anterior; el script usa `major*10000 + minor*100 + patch`.

Requisitos técnicos ya comprobados: `targetSdkVersion` 36, `minSdkVersion` 29, solo arm64, todas las librerías nativas alineadas a 16 KB (también las de `server.zip`), tamaño del bundle 78 MB (el límite es 200 MB).

## Pasos en Play Console

1. **Cuenta de desarrollador**: pago único de 25 USD y verificación de identidad. Si es cuenta personal creada después de noviembre de 2023, Play exige una prueba cerrada con al menos 12 testers durante 14 días seguidos antes de poder publicar en producción.
2. **Crear la app**: nombre `TerraNet`, idioma predeterminado español (Latinoamérica) o inglés, tipo **App** (no juego), gratis.
3. **Firma de la app (Play App Signing)**: elige *usar tu propia clave* y sube `secrets/terranet.keystore` con la herramienta PEPK que te da la consola. Así la versión de Play tiene la misma firma que la APK de la web y quien ya la tiene instalada puede pasar a la de Play sin desinstalar. Si dejas que Google genere la clave, las dos versiones no se podrán actualizar entre sí.
4. **Ficha de la tienda**: textos en `listing-es.md` y `listing-en.md`; ícono `graphics/icon-512.png`; gráfico destacado `graphics/feature-es.png` o `feature-en.png`; capturas en `screenshots/es` y `screenshots/en` (1080x2160).
5. **Política de privacidad**: `https://terra.hacks.win/privacy` (español e inglés). Los términos de uso están en `https://terra.hacks.win/terms`. Ninguna de las dos aparece enlazada en la portada. `privacy.md` tiene el mismo texto.
6. **Seguridad de los datos**: respuestas en `data-safety.md`.
7. **Clasificación de contenido** (cuestionario IARC): categoría *Utilidad, productividad, comunicación u otra*. Sin violencia, sexo, drogas ni apuestas. A la pregunta de si los usuarios pueden interactuar entre sí, responde **sí** (los jugadores se conectan al servidor y pueden chatear dentro de Terraria). No comparte la ubicación del usuario. No hay compras.
8. **Público objetivo**: 13 años o más. Si incluyes menores de 13 entra en la política de familias y piden mucho más.
9. **Anuncios**: no contiene anuncios.
10. **Acceso a la app**: toda la funcionalidad está disponible sin iniciar sesión.
11. **Permisos de servicio en primer plano**: Play pide describir el uso y un **video** corto (YouTube, puede ser oculto) que lo muestre. Texto sugerido:
    > TerraNet hosts a Terraria multiplayer server on the device. The server keeps running in a foreground service with a persistent notification while friends are connected, so the world stays online when the user switches apps or turns off the screen. The user starts it by opening a world and stops it from the app or the notification. The same service also runs the optional console bridge, which the user turns on to relay a game server to the consoles on their local network; it keeps the relay alive with the same notification until the user turns it off.

    El video: abrir un mundo, mostrar la notificación "en línea", salir de la app y mostrar que un amigo sigue conectado, y detenerlo desde la notificación.
12. **Optimización de batería**: la app pide `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS` la primera vez que un mundo queda en línea, porque el servidor tiene que mantener conexiones de red persistentes y Doze las corta. Es uno de los casos aceptados por Play (la función principal se rompe sin la excepción). Si en la revisión lo objetan, la alternativa es quitar el permiso y abrir solo la pantalla de ajustes de batería.
13. **Prueba cerrada**: sube el `.aab` a una pista de prueba cerrada, agrega a los 12 testers por correo o con un grupo de Google y espera los 14 días. Después solicitas acceso a producción.

## Riesgos que quedan

- **Marca Terraria**: la app lleva el servidor dedicado de Terraria (vía TShock y OTAPI). La ficha dice que es compatible con Terraria y que no está afiliada a Re-Logic, y no usa su logo ni capturas del juego. Aun así, si Re-Logic reclama, Google puede retirar la app. Lo más seguro es escribirles antes.
- **Plugins**: el usuario importa DLLs de TShock desde sus archivos. Es el mismo caso que los IDEs o TL Pro que están en Play, pero no conviene agregar un catálogo que descargue plugins desde un servidor.
