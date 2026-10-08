using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Workflow;

// Schema generation does not connect to a database or require organization credentials.
public sealed class DesignTimeDb : IDesignTimeDbContextFactory<WorkflowDb>
{
    public WorkflowDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<WorkflowDb>()
        .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Workflow;Trusted_Connection=True;TrustServerCertificate=True").Options);
}
