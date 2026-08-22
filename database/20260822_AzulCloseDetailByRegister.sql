SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.ProcessCierreLote', 'Caja') IS NULL
    ALTER TABLE dbo.ProcessCierreLote ADD Caja int NULL;
IF COL_LENGTH('dbo.ProcessCierreLote', 'detailType') IS NULL
    ALTER TABLE dbo.ProcessCierreLote ADD detailType varchar(10) NULL;
IF COL_LENGTH('dbo.ProcessCierreLote', 'detailAmount') IS NULL
    ALTER TABLE dbo.ProcessCierreLote ADD detailAmount numeric(12,2) NULL;
IF COL_LENGTH('dbo.ProcessCierreLote', 'detailDateTime') IS NULL
    ALTER TABLE dbo.ProcessCierreLote ADD detailDateTime smalldatetime NULL;
IF COL_LENGTH('dbo.ProcessCierreLote', 'detailCardLast4') IS NULL
    ALTER TABLE dbo.ProcessCierreLote ADD detailCardLast4 varchar(4) NULL;
IF COL_LENGTH('dbo.ProcessCierreLote', 'detailAuthorization') IS NULL
    ALTER TABLE dbo.ProcessCierreLote ADD detailAuthorization varchar(20) NULL;
IF COL_LENGTH('dbo.ProcessCierreLote', 'detailDcc') IS NULL
    ALTER TABLE dbo.ProcessCierreLote ADD detailDcc varchar(1) NULL;

COMMIT TRANSACTION;
GO

ALTER PROCEDURE dbo.CierresLote
    @IpRemota nvarchar(50),
    @CloseResponse nvarchar(max),
    @Caja int = NULL,
    @Usuario varchar(3) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.ProcessCierreLote
        (IpRemota, CloseResponse, ExecutionTime, Caja, usuario, procesado)
    VALUES
        (@IpRemota, @CloseResponse, NULL, @Caja, @Usuario, 0);
END;
GO

ALTER PROCEDURE dbo.GuardaCierresLote
    @IpRemota nvarchar(50),
    @CloseResponse nvarchar(max),
    @ExecutionTime datetime = NULL,
    @IDComunicacion int = NULL OUTPUT,
    @Estatus varchar(20) = NULL,
    @TerminalId varchar(20) = NULL,
    @FechaHora smalldatetime = NULL,
    @Quantity int = NULL,
    @Amount numeric(12,2) = NULL,
    @CancelQuantity int = NULL,
    @CancelAmount numeric(12,2) = NULL,
    @Caja int = NULL,
    @Usuario varchar(3) = NULL,
    @DetailType varchar(10) = NULL,
    @DetailAmount numeric(12,2) = NULL,
    @DetailDateTime smalldatetime = NULL,
    @DetailCardLast4 varchar(4) = NULL,
    @DetailAuthorization varchar(20) = NULL,
    @DetailDcc varchar(1) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @IDComunicacion IS NULL
    BEGIN
        INSERT INTO dbo.ProcessCierreLote
            (IpRemota, CloseResponse, ExecutionTime, Caja, usuario, procesado)
        VALUES
            (@IpRemota, @CloseResponse, @ExecutionTime, @Caja, @Usuario, 0);

        SET @IDComunicacion = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.ProcessCierreLote
        SET ExecutionTime = COALESCE(@ExecutionTime, ExecutionTime, GETDATE()),
            CloseResponse = @CloseResponse,
            estatus = COALESCE(@Estatus, estatus),
            terminalID = COALESCE(@TerminalId, terminalID),
            fechahora = COALESCE(@FechaHora, fechahora),
            quantity = COALESCE(@Quantity, quantity),
            amount = COALESCE(@Amount, amount),
            cancelQuantity = COALESCE(@CancelQuantity, cancelQuantity),
            cancelAmount = COALESCE(@CancelAmount, cancelAmount),
            Caja = COALESCE(@Caja, Caja),
            usuario = COALESCE(@Usuario, usuario),
            detailType = COALESCE(@DetailType, detailType),
            detailAmount = COALESCE(@DetailAmount, detailAmount),
            detailDateTime = COALESCE(@DetailDateTime, detailDateTime),
            detailCardLast4 = COALESCE(@DetailCardLast4, detailCardLast4),
            detailAuthorization = COALESCE(@DetailAuthorization, detailAuthorization),
            detailDcc = COALESCE(@DetailDcc, detailDcc),
            procesado = 1
        WHERE Id = @IDComunicacion;
    END;
END;
GO
