# Formulario de seguridad de los datos

Respuestas para la sección *Seguridad de los datos* de Play Console, basadas en lo que hace el código de esta rama.

## Recopilación y uso compartido

- **¿La app recopila o comparte alguno de los tipos de datos requeridos?** Sí.
- **¿Todos los datos se encriptan en tránsito?** Sí (la conexión al relay usa TLS con el certificado fijado en la app).
- **¿Ofreces una forma de solicitar que se borren los datos?** Sí, por el contacto de la política de privacidad. Además, los registros sin uso se borran solos a los 14 días.

## Tipos de datos

| Categoría | Tipo | Recopilado | Compartido | Opcional | Finalidad |
| --- | --- | --- | --- | --- | --- |
| Identificadores de dispositivo u otros | ID de dispositivo u otros identificadores | Sí | No | No (sin él no hay dirección pública) | Funcionalidad de la app |

No marques ninguno de estos: ubicación, información personal (nombre, correo, etc.), información financiera, salud, mensajes, fotos y videos, audio, archivos y documentos, calendario, contactos, actividad en la app, navegación web, información y rendimiento de la app (no hay analítica ni informes de fallos enviados).

Notas:
- El identificador es aleatorio, lo genera la app y no está ligado a la cuenta de Google ni al hardware.
- La IP de conexión al relay no se usa para ubicar al usuario; Play no la trata como tipo de dato aparte si no se usa para derivar ubicación.
- El tráfico del juego solo se reenvía al pasar (no se guarda), lo que Play considera *procesamiento efímero*.
- Las cuentas y personajes de los jugadores en el servidor de TShock se guardan solo en el dispositivo y no salen de él, así que no cuentan como recopilados.

## Otras declaraciones

- **Anuncios**: no.
- **Compras dentro de la app**: no.
- **ID de publicidad**: no se usa (declara que la app no usa el ID de publicidad).
- **Acceso a la app**: no requiere inicio de sesión.
- **Apps gubernamentales, financieras, de salud o noticias**: no.
