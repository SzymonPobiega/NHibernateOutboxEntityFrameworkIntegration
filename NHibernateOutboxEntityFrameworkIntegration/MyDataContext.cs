using System.Data;
using System.Data.Common;
using System.Data.Entity;

class MyDataContext : DbContext
{
    public MyDataContext(DbConnection connection) : base(connection, false)
    {
    }

    public DbSet<MyEntity> MyEntities { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var orders = modelBuilder.Entity<MyEntity>();
        orders.ToTable("MyEntity");
        orders.HasKey(x => x.Id);
        orders.Property(x => x.Value);
    }
}