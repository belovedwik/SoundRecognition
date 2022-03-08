CREATE PROCEDURE EnableMsSpeech AS
BEGIN 
DECLARE @PATTERN  NVARCHAR(147)
SET @PATTERN = '{"AllowAutodetect":true,"AutodetectConf":"{\"BeforeConnection\":{\"Enabled\":true,\"Metod\":3},\"AfterConnection\":{\"Enabled\":true,\"Metod\":3}}"'

UPDATE A
SET A.Settings = CONCAT(@PATTERN,SUBSTRING(Settings,148,LEN(Settings)))
FROM [dbo].[AlgoritmSettings] A
JOIN [dbo].[WorkQueues] W ON A.QueueId = W.Id
WHERE W.IsDeleted = 0 and W.IsIncoming = 0 and (A.Settings like '{"AllowAutodetect":true%' )
END

GO

CREATE PROCEDURE EnableHardPartial AS
BEGIN 
DECLARE @PATTERN  NVARCHAR(147)
SET @PATTERN = '{"AllowAutodetect":true,"AutodetectConf":"{\"BeforeConnection\":{\"Enabled\":true,\"Metod\":0},\"AfterConnection\":{\"Enabled\":true,\"Metod\":2}}"'

UPDATE A
SET A.Settings = CONCAT(@PATTERN,SUBSTRING(Settings,148,LEN(Settings)))
FROM [dbo].[AlgoritmSettings] A
JOIN [dbo].[WorkQueues] W ON A.QueueId = W.Id
WHERE W.IsDeleted = 0 and W.IsIncoming = 0 and (A.Settings like '{"AllowAutodetect":true%' )
END