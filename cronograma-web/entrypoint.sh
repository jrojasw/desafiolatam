#!/bin/sh

DB_PATH="/app/cronograma.db"

if [ -n "$LITESTREAM_BUCKET" ]; then
    echo "Litestream configurado: restaurando base de datos desde el respaldo (si existe)..."
    litestream restore -if-replica-exists -config /etc/litestream.yml "$DB_PATH" \
        || echo "Advertencia: no se pudo restaurar el respaldo (revisa Litestream/Backblaze). Se continúa igual con la base de datos local."

    litestream replicate -config /etc/litestream.yml &
    exec dotnet CronogramaTrabajo.Web.dll
else
    echo "LITESTREAM_BUCKET no configurado: iniciando sin respaldo continuo."
    exec dotnet CronogramaTrabajo.Web.dll
fi
