using System.Text;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class EmployeeHrPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task EmployeeDocumentsAndCertificatesAreStoredAndAdminOnly()
    {
        var database = postgres.CreateDatabase();
        Guid employeeId;
        await using (var seed = database.CreateDbContext())
        {
            var employee = NewEmployee();
            seed.Employees.Add(employee);
            await seed.SaveChangesAsync();
            employeeId = employee.Id;
        }

        await using var db = database.CreateDbContext();
        var admin = CurrentUser(isAdmin: true);
        var storage = new MemoryEmployeeDocumentStorage();
        var service = new EmployeeDocumentService(
            db, storage, admin.Object, NullLogger<EmployeeDocumentService>.Instance);
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\ntraining");
        var created = await service.CreateAsync(
            employeeId,
            new(
                "SafetyTraining",
                "Formazione sicurezza",
                "SAF-2026-001",
                "Ente formatore",
                null,
                new DateOnly(2026, 11, 4),
                new DateOnly(2028, 11, 4),
                "Aggiornamento biennale"),
            new MemoryStream(pdf),
            "attestato.pdf",
            "application/pdf",
            pdf.Length,
            "admin-user",
            CancellationToken.None);

        Assert.True(created.Success, created.Error);
        Assert.Equal("attestato.pdf", created.Data!.FileName);
        Assert.Single((await service.ListAsync(employeeId, 1, 20, CancellationToken.None)).Items);
        var downloaded = await service.DownloadAsync(employeeId, created.Data.Id, CancellationToken.None);
        Assert.NotNull(downloaded);
        using (downloaded!.Value.Stream)
        {
            using var buffer = new MemoryStream();
            await downloaded.Value.Stream.CopyToAsync(buffer);
            Assert.Equal(pdf, buffer.ToArray());
        }

        var accounting = CurrentUser(isAdmin: false);
        var restricted = new EmployeeDocumentService(
            db, storage, accounting.Object, NullLogger<EmployeeDocumentService>.Instance);
        Assert.Empty((await restricted.ListAsync(employeeId, 1, 20, CancellationToken.None)).Items);
        Assert.Null(await restricted.DownloadAsync(employeeId, created.Data.Id, CancellationToken.None));
    }

    [Fact]
    public async Task TimeEntriesRecordWorkedMinutesAndRejectDuplicateEmployeeDay()
    {
        var database = postgres.CreateDatabase();
        Guid employeeId;
        await using (var seed = database.CreateDbContext())
        {
            var employee = NewEmployee();
            seed.Employees.Add(employee);
            await seed.SaveChangesAsync();
            employeeId = employee.Id;
        }

        await using var db = database.CreateDbContext();
        var admin = CurrentUser(isAdmin: true);
        var service = new EmployeeTimeEntryService(
            db, admin.Object, NullLogger<EmployeeTimeEntryService>.Instance);
        var work = await service.CreateAsync(
            employeeId,
            new(
                new DateOnly(2026, 11, 4),
                "Work",
                new TimeOnly(8, 30),
                new TimeOnly(17, 0),
                30,
                null),
            "admin-user",
            CancellationToken.None);

        Assert.True(work.Success, work.Error);
        Assert.Equal(480, work.Data!.WorkedMinutes);
        var duplicate = await service.CreateAsync(
            employeeId,
            new(
                new DateOnly(2026, 11, 4),
                "Work",
                new TimeOnly(9, 0),
                new TimeOnly(17, 0),
                30,
                null),
            "admin-user",
            CancellationToken.None);
        Assert.False(duplicate.Success);
        Assert.Contains("già un cartellino", duplicate.Error, StringComparison.OrdinalIgnoreCase);

        var vacation = await service.CreateAsync(
            employeeId,
            new(new DateOnly(2026, 11, 5), "Vacation", null, null, 0, "Ferie"),
            "admin-user",
            CancellationToken.None);
        Assert.True(vacation.Success, vacation.Error);
        Assert.Equal(0, vacation.Data!.WorkedMinutes);
        Assert.Equal(2, (await service.ListAsync(
            employeeId,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30),
            1,
            31,
            CancellationToken.None)).TotalCount);
    }

    private static Employee NewEmployee()
    {
        var employee = new Employee();
        var result = employee.Update(
            "MAT-HR-001",
            "Mario",
            "Rossi",
            "Impiegato",
            "4",
            "Commercio",
            null,
            45_000m,
            new DateOnly(2024, 1, 1),
            null,
            true,
            null);
        Assert.True(result.Success, result.Error);
        return employee;
    }

    private static Mock<ICurrentUserService> CurrentUser(bool isAdmin)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(current => current.UserId).Returns("admin-user");
        user.Setup(current => current.FullName).Returns("Amministratore");
        user.Setup(current => current.HasRole("Admin")).Returns(isAdmin);
        return user;
    }

    private sealed class MemoryEmployeeDocumentStorage : IEmployeeDocumentStorage
    {
        private readonly Dictionary<string, (byte[] Bytes, string ContentType)> files = [];

        public async Task<string> UploadAsync(
            Guid employeeId,
            Guid documentId,
            Stream stream,
            string contentType,
            CancellationToken cancellationToken)
        {
            var key = $"employees/{employeeId:N}/documents/{documentId:N}";
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            files.Add(key, (buffer.ToArray(), contentType));
            return key;
        }

        public Task<(Stream Stream, string ContentType)> DownloadAsync(
            string key,
            CancellationToken cancellationToken)
        {
            var file = files[key];
            return Task.FromResult<(Stream, string ContentType)>(
                (new MemoryStream(file.Bytes), file.ContentType));
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            files.Remove(key);
            return Task.CompletedTask;
        }
    }
}
