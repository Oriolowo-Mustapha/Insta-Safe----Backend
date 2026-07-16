$ErrorActionPreference = "Stop"

Set-Location "C:\Users\MUSTAPHA\source\repos\Instasafe -- Backend"

dotnet new classlib -n InstaSafe.Domain -o src/InstaSafe.Domain --framework net10.0
dotnet new classlib -n InstaSafe.Application -o src/InstaSafe.Application --framework net10.0
dotnet new classlib -n InstaSafe.Infrastructure -o src/InstaSafe.Infrastructure --framework net10.0
dotnet new webapi -n InstaSafe.Api -o src/InstaSafe.Api --framework net10.0 --no-https false

dotnet new xunit -n InstaSafe.Domain.UnitTests -o tests/InstaSafe.Domain.UnitTests --framework net10.0
dotnet new xunit -n InstaSafe.Application.UnitTests -o tests/InstaSafe.Application.UnitTests --framework net10.0
dotnet new xunit -n InstaSafe.Api.IntegrationTests -o tests/InstaSafe.Api.IntegrationTests --framework net10.0

# If .slnx is not supported by dotnet slnx yet (it's new in .NET 9/10), we might need to use dotnet sln. Let's try dotnet slnx first, fallback to standard sln.
try {
    dotnet slnx add "Instasafe -- Backend.slnx" src/InstaSafe.Domain/InstaSafe.Domain.csproj
    dotnet slnx add "Instasafe -- Backend.slnx" src/InstaSafe.Application/InstaSafe.Application.csproj
    dotnet slnx add "Instasafe -- Backend.slnx" src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj
    dotnet slnx add "Instasafe -- Backend.slnx" src/InstaSafe.Api/InstaSafe.Api.csproj
    dotnet slnx add "Instasafe -- Backend.slnx" tests/InstaSafe.Domain.UnitTests/InstaSafe.Domain.UnitTests.csproj
    dotnet slnx add "Instasafe -- Backend.slnx" tests/InstaSafe.Application.UnitTests/InstaSafe.Application.UnitTests.csproj
    dotnet slnx add "Instasafe -- Backend.slnx" tests/InstaSafe.Api.IntegrationTests/InstaSafe.Api.IntegrationTests.csproj
} catch {
    Write-Host "dotnet slnx failed, creating standard .sln instead..."
    dotnet new sln -n InstaSafe
    dotnet sln InstaSafe.sln add src/InstaSafe.Domain/InstaSafe.Domain.csproj
    dotnet sln InstaSafe.sln add src/InstaSafe.Application/InstaSafe.Application.csproj
    dotnet sln InstaSafe.sln add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj
    dotnet sln InstaSafe.sln add src/InstaSafe.Api/InstaSafe.Api.csproj
    dotnet sln InstaSafe.sln add tests/InstaSafe.Domain.UnitTests/InstaSafe.Domain.UnitTests.csproj
    dotnet sln InstaSafe.sln add tests/InstaSafe.Application.UnitTests/InstaSafe.Application.UnitTests.csproj
    dotnet sln InstaSafe.sln add tests/InstaSafe.Api.IntegrationTests/InstaSafe.Api.IntegrationTests.csproj
}

dotnet add src/InstaSafe.Application/InstaSafe.Application.csproj reference src/InstaSafe.Domain/InstaSafe.Domain.csproj
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj reference src/InstaSafe.Application/InstaSafe.Application.csproj
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj reference src/InstaSafe.Domain/InstaSafe.Domain.csproj
dotnet add src/InstaSafe.Api/InstaSafe.Api.csproj reference src/InstaSafe.Application/InstaSafe.Application.csproj
dotnet add src/InstaSafe.Api/InstaSafe.Api.csproj reference src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj

dotnet add src/InstaSafe.Domain/InstaSafe.Domain.csproj package MediatR.Contracts

dotnet add src/InstaSafe.Application/InstaSafe.Application.csproj package MediatR
dotnet add src/InstaSafe.Application/InstaSafe.Application.csproj package FluentValidation
dotnet add src/InstaSafe.Application/InstaSafe.Application.csproj package FluentValidation.DependencyInjectionExtensions
dotnet add src/InstaSafe.Application/InstaSafe.Application.csproj package Microsoft.Extensions.DependencyInjection.Abstractions

dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Tools
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package Hangfire.Core
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package Hangfire.SqlServer
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package Hangfire.AspNetCore
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package Polly
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package Microsoft.Extensions.Http.Polly
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package Serilog.AspNetCore
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package Serilog.Sinks.Console
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package Serilog.Sinks.File
dotnet add src/InstaSafe.Infrastructure/InstaSafe.Infrastructure.csproj package QRCoder

dotnet add src/InstaSafe.Api/InstaSafe.Api.csproj package Swashbuckle.AspNetCore
dotnet add src/InstaSafe.Api/InstaSafe.Api.csproj package Serilog.AspNetCore
dotnet add src/InstaSafe.Api/InstaSafe.Api.csproj package Hangfire.AspNetCore

Remove-Item src/InstaSafe.Domain/Class1.cs -ErrorAction SilentlyContinue
Remove-Item src/InstaSafe.Application/Class1.cs -ErrorAction SilentlyContinue
Remove-Item src/InstaSafe.Infrastructure/Class1.cs -ErrorAction SilentlyContinue

Write-Host "Setup completed successfully."
