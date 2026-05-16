$conn = New-Object System.Data.SqlClient.SqlConnection("Server=db45683.public.databaseasp.net; Database=db45683; User Id=db45683; Password=Nf8=-Xj2y5A!; Encrypt=True; TrustServerCertificate=True;")
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TOP 1 * FROM Exams"
    $reader = $cmd.ExecuteReader()
    for ($i=0; $i -lt $reader.FieldCount; $i++) {
        Write-Host $reader.GetName($i)
    }
} catch {
    Write-Error $_.Exception.Message
} finally {
    $conn.Close()
}
