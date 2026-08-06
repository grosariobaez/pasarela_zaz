# Pasarela ZAZ

## Nombre y propósito

Aplicación de consola que funciona como pasarela entre EasyPOS, SQL Server y los proveedores de pago Cardnet o Azul. Cada instancia procesa exclusivamente el proveedor indicado al ejecutarla.

## Tecnología

- C# y .NET 6
- SQL Server
- Newtonsoft.Json
- Serilog
- ECRti.Framework para Cardnet
- WebAPI local de Azul para Ingenico Lane 7000

## Proveedores

### Cardnet

Mantiene la integración existente mediante `ECRti.Framework`, que se comunica con los terminales configurados por la aplicación.

### Azul

El flujo de comunicación es:

```text
EasyPOS
  -> aplicación de consola
  -> WebAPI Azul en http://localhost:9000
  -> Ingenico Lane 7000
```

La aplicación no abre directamente el puerto COM. El servicio local de Azul es responsable de comunicarse con el terminal.

## Operaciones soportadas

| Operación | Cardnet | Azul |
| --- | ---: | ---: |
| Venta | Sí | Sí |
| Venta en cuotas | Sí | No |
| Consulta de última transacción | Sí | No |
| Anulación | Sí | Sí |
| Cierre | Sí | Sí |
| Devolución | No documentada | No implementada |

## Ejecución

El modo unificado es el formato recomendado. El proveedor es obligatorio y solo admite `Cardnet` o `Azul`:

```text
EasyPOS_Cardnet <destino> <proveedor>
```

Ejemplos genéricos:

```text
EasyPOS_Cardnet <destino> Cardnet
EasyPOS_Cardnet <destino> Azul
```

Cada instancia unificada consulta las colas de `Ventas`, `Cancelaciones` y `Cierres` del proveedor indicado. En ventas, el destino `All` solicita al procedimiento existente que consulte todos los destinos; cualquier otro valor se envía como identificador de destino a SQL Server.

Una ejecución atiende un solo proveedor. No existe proveedor predeterminado y una invocación con argumentos ausentes, adicionales o inválidos termina con error.

El formato anterior permanece disponible temporalmente para facilitar la transición:

```text
EasyPOS_Cardnet <destino> <operación> <proveedor>
```

Las operaciones admitidas en ese formato son `Ventas`, `Cancelaciones` y `Cierres`.

### Comportamiento de la ejecución

- El modo unificado mantiene tres trabajadores: ventas consulta cada segundo; cancelaciones y cierres, cada tres segundos.
- Ventas tiene prioridad. Un bloqueo compartido garantiza que solo una operación se comunique con el terminal a la vez.
- Los trabajadores continúan consultando sus colas hasta detener el proceso con `Ctrl+C`.
- Cada consulta procesa todas las filas pendientes que SQL Server devuelva para el destino; no se limita a una sola transacción.
- Antes de una prueba controlada se debe confirmar que no existan otras operaciones pendientes para el mismo destino.
- Una instancia iniciada para Cardnet nunca procesa operaciones mediante Azul, y viceversa.

## Configuración Azul

- Base URL: `http://localhost:9000`
- Timeout: 45 segundos
- Sin reintentos automáticos
- Comunicación mediante HTTP GET y respuestas JSON
- Estados: `00` indica éxito, `01` rechazo o fallo operativo y `99` error técnico o respuesta incompleta

## Operaciones Azul

```text
/api/transaction/lane/sale/{Amount}
/api/transaction/lane/Void/{InvoiceNumber}
/api/transaction/lane/CloseTotals
```

Los importes se envían con punto decimal y dos posiciones. Azul no implementa venta en cuotas, consulta de última transacción ni devolución.

`Procesar_POS` entrega el monto en unidades menores. La ruta de venta Azul divide ese valor entre 100 y después lo formatea con `CultureInfo.InvariantCulture`; por ejemplo, el valor SQL `36000` se envía como `360.00`. Esta conversión está limitada a Azul y no modifica el flujo Cardnet.

### Tratamiento de respuestas Azul

- Una respuesta HTTP exitosa debe contener JSON válido.
- `TransactionOverallStatus = "00"` se interpreta como éxito y la venta Azul se guarda en `Procesador_pagos.Estatus` como `Successful`.
- `TransactionOverallStatus = "01"` se registra como rechazo comercial; no es una excepción técnica.
- Timeout, servicio no disponible, HTTP no exitoso, JSON inválido o ausencia de `TransactionOverallStatus` se registran con estado `99`.
- Cualquier otro estado recibido se conserva como resultado no exitoso sin provocar una caída innecesaria.
- La aplicación no realiza reintentos automáticos.

## Base de datos

La solución reutiliza la estructura y las colas existentes de SQL Server mediante estos procedimientos:

- `Procesar_POS`
- `Procesar_POS_All`
- `Procesa_POS_Res`
- `dbo.Get_Cancelaciones`
- `Voucher_SaveCanceledresult`
- `dbo.Get_Cierres`
- `GuardaCierresLote`

Las respuestas completas de Azul se conservan en los campos existentes destinados a la trama o resultado de cada operación.

La columna existente `Procesador_pagos.Company` identifica el procesador utilizado. Las ventas nuevas se guardan con `Azul` o `Cardnet`; los registros históricos anteriores a esta integración pueden permanecer vacíos.

### Mapeo de persistencia Azul

La venta Azul reutiliza `Procesa_POS_Res`. Los campos con equivalencia se extraen del JSON, incluyendo PAN enmascarado, lote, autorización, modo de entrada, fecha/hora, AID, titular, terminal, comercio y moneda. `InvoiceNumber` se guarda en `Reference` y `TransactionReference` en `rnn`. Los campos sin equivalencia se envían vacíos. El JSON completo se guarda como trama recibida.

Las anulaciones reutilizan `Voucher_SaveCanceledresult` y los cierres reutilizan `GuardaCierresLote`. En `Cancelacion_Venta`, el flujo Azul conserva el JSON completo y guarda por separado el procesador, estado, referencia financiera, monto, autorización, respuestas host/terminal, modo de entrada, lote, fecha/hora, terminal, comercio y producto. No se duplican PAN, criptogramas ni recibos en columnas estructuradas.

En cierres Azul, el JSON original permanece en `CloseResponse`. El resumen incluido en `Receipts` se parsea para guardar en `ProcessCierreLote` el estado, terminal, fecha/hora, cantidad e importe bruto de ventas, además de la cantidad e importe de anulaciones. El total neto puede obtenerse como `amount - cancelAmount`.

## Organización del código

La solución conserva toda la lógica en `Program.cs`, organizada funcionalmente en estos grupos:

- Inicio, validación de argumentos y selección exclusiva del proveedor.
- Lectura de colas y procesamiento Cardnet mediante `ECRti.Framework`.
- Lectura de colas y operaciones Azul mediante la WebAPI local.
- Adaptación y persistencia de respuestas en los procedimientos SQL existentes.
- Cierres, consultas auxiliares y apagado de la aplicación.
- `AzulHttpResult`, contenedor interno para diferenciar JSON válido de fallos técnicos.

Archivos principales:

- `Program.cs`: flujo completo de la pasarela.
- `EasyPOS_Cardnet.csproj`: plataforma .NET 6 y dependencias.
- `appsettings.json`: configuración de logging distribuida con la aplicación.
- `lib/ECRti.Framework.dll`: biblioteca utilizada por la integración Cardnet.

## Compilación

Desde la raíz del repositorio:

```powershell
dotnet build EasyPOS_Cardnet.sln -t:Rebuild
```

La dependencia `ECRti.Framework` puede producir la advertencia `NU1701` porque fue publicada para .NET Framework y se restaura en el proyecto `net6.0`. Esta advertencia ya existe en la solución y no impide la compilación actual.

## Operación del servicio Azul

Antes de procesar operaciones Azul:

```powershell
Get-Service AZUL.Ingenico.WebAPI
Test-NetConnection 127.0.0.1 -Port 9000
```

El servicio debe aparecer en estado `Running` y la prueba TCP debe devolver `TcpTestSucceeded: True`. El puerto COM del Lane 7000 pertenece a la configuración del servicio Azul; no se pasa como argumento ni se abre desde esta aplicación.

## Riesgos aceptados

1. `Procesador_pagos.Reference` y `Cancelacion_Venta.ReferenceNumber` deben permanecer como `varchar(50)` para conservar el `InvoiceNumber` de Azul, incluidos sus ceros iniciales.
2. Cardnet continúa validando y convirtiendo su referencia numérica antes de comunicarse con `ECRti.Framework`.
3. La respuesta original de cada procesador permanece en `Trama_Recibida` para auditoría.

## Pruebas

Las pruebas con terminales y transacciones deben realizarse manualmente en un ambiente controlado. Antes de iniciar, se deben revisar las colas pendientes y usar exclusivamente datos y montos de prueba autorizados.

Flujo recomendado para una prueba controlada:

1. Confirmar que el servicio Azul y el puerto TCP 9000 estén activos.
2. Verificar que no haya otras filas pendientes para el destino.
3. Crear una sola operación autorizada.
4. Ejecutar la aplicación con el proveedor correcto.
5. Comparar el importe mostrado por el Lane con el importe esperado.
6. Validar el estado registrado en SQL y conservar la salida de consola.
7. Detener una ejecución de `Ventas` con `Ctrl+C` antes de preparar otra fila.

Se validó manualmente que un monto de venta Azul se muestra con dos decimales en el Lane 7000, que un rechazo comercial se registra como `01` y que un timeout de 45 segundos se registra como `99`.
