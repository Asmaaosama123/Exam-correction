$conn = New-Object System.Data.SqlClient.SqlConnection("Server=db45683.public.databaseasp.net; Database=db45683; User Id=db45683; Password=Nf8=-Xj2y5A!; Encrypt=True; TrustServerCertificate=True;")
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Id, Name, OwnerId FROM Exams WHERE OwnerId IN ('019dc5e1-3304-7a3e-bfd7-f742f61db171', '019dc5ef-ddd9-712e-91cb-fb92d97fef8e')"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host "Exam: $($reader['Name']) (ID: $($reader['Id'])) owned by $($reader['OwnerId'])"
    }
} catch {
    Write-Error $_.Exception.Message
} finally {
    $conn.Close()
}
