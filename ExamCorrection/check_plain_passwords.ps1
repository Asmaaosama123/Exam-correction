$conn = New-Object System.Data.SqlClient.SqlConnection("Server=db45683.public.databaseasp.net; Database=db45683; User Id=db45683; Password=Nf8=-Xj2y5A!; Encrypt=True; TrustServerCertificate=True;")
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Email, PlainPassword FROM AspNetUsers WHERE Email IN ('menasalehalex@gmail.com', 'rogenayousryadel12345@gmail.com')"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host "$($reader['Email']) -> [$($reader['PlainPassword'])]"
    }
} catch {
    Write-Error $_.Exception.Message
} finally {
    $conn.Close()
}
