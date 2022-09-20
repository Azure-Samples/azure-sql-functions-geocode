CREATE TRIGGER NEW_WORKORDER
ON dbo.Workorders
AFTER INSERT
AS
BEGIN

    DECLARE @PAYLOAD NVARCHAR(MAX)
    DECLARE @WORKORDER_ID UNIQUEIDENTIFIER

    SELECT @WORKORDER_ID = [Id]
    FROM inserted WHERE Latitude IS NULL

    IF @WORKORDER_ID IS NOT NULL
    BEGIN

        SET @PAYLOAD = (SELECT [Id], [Summary], [Description], [Location] FROM inserted
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)

        BEGIN TRY
            DECLARE @URL NVARCHAR(1000) = '$(WorkorderFunctionEndpoint)'
            DECLARE @response nvarchar(MAX)

            exec sp_invoke_external_rest_endpoint @url = @URL
                , @payload = @PAYLOAD
                , @headers =  '{ "Content-Type": "application/json" }'
                , @method = 'POST'
                , @response = @response OUTPUT;
        END TRY
        BEGIN CATCH
            UPDATE Workorders
            SET Latitude = 0
                , Longitude = 0
            WHERE Id = @WORKORDER_ID;
        END CATCH
    END
END