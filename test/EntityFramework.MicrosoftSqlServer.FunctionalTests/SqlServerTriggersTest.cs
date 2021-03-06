﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Data.Entity.FunctionalTests;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Data.Entity.SqlServer.FunctionalTests
{
    public class SqlServerTriggersTest : IClassFixture<SqlServerTriggersTest.SqlServerTriggersFixture>, IDisposable
    {
        [Fact]
        public void Triggers_run_on_insert_update_and_delete()
        {
            using (var context = CreateContext())
            {
                var product = new Product { Name = "blah" };
                context.Products.Add(product);
                context.SaveChanges();

                var firstVersion = product.Version;
                var productBackup = context.ProductBackups.AsNoTracking().Single();
                Assert.Equal(product.Id, productBackup.Id);
                Assert.Equal(product.Name, productBackup.Name);
                Assert.Equal(product.Version, productBackup.Version);

                product.Name = "fooh";
                context.SaveChanges();

                Assert.NotEqual(firstVersion, product.Version);
                productBackup = context.ProductBackups.AsNoTracking().Single();
                Assert.Equal(product.Id, productBackup.Id);
                Assert.Equal(product.Name, productBackup.Name);
                Assert.Equal(product.Version, productBackup.Version);

                context.Products.Remove(product);
                context.SaveChanges();

                Assert.Empty(context.Products);
                Assert.Empty(context.ProductBackups);
            }
        }

        [Fact]
        public void Triggers_work_with_batch_operations()
        {
            using (var context = CreateContext())
            {
                var productToBeUpdated1 = new Product { Name = "u1" };
                var productToBeUpdated2 = new Product { Name = "u2" };
                context.Products.Add(productToBeUpdated1);
                context.Products.Add(productToBeUpdated2);

                var productToBeDeleted1 = new Product { Name = "d1" };
                var productToBeDeleted2 = new Product { Name = "d2" };
                context.Products.Add(productToBeDeleted1);
                context.Products.Add(productToBeDeleted2);

                context.SaveChanges();

                var productToBeAdded1 = new Product { Name = "a1" };
                var productToBeAdded2 = new Product { Name = "a2" };
                context.Products.Add(productToBeAdded1);
                context.Products.Add(productToBeAdded2);

                productToBeUpdated1.Name = "n1";
                productToBeUpdated2.Name = "n2";

                context.Products.Remove(productToBeDeleted1);
                context.Products.Remove(productToBeDeleted2);

                context.SaveChanges();

                var productBackups = context.ProductBackups.ToList();

                Assert.Equal(4, productBackups.Count);
                Assert.True(productBackups.Any(p => p.Name == "a1"));
                Assert.True(productBackups.Any(p => p.Name == "a2"));
                Assert.True(productBackups.Any(p => p.Name == "n1"));
                Assert.True(productBackups.Any(p => p.Name == "n2"));
            }
        }

        private readonly SqlServerTriggersFixture _fixture;
        private readonly SqlServerTestStore _testStore;

        public SqlServerTriggersTest(SqlServerTriggersFixture fixture)
        {
            _fixture = fixture;
            _testStore = _fixture.GetTestStore();
        }

        private TriggersContext CreateContext() => _fixture.CreateContext(_testStore);

        public void Dispose() => _testStore.Dispose();

        public class SqlServerTriggersFixture
        {
            private readonly IServiceProvider _serviceProvider;

            public SqlServerTriggersFixture()
            {
                _serviceProvider
                    = new ServiceCollection()
                        .AddEntityFramework()
                        .AddSqlServer()
                        .ServiceCollection()
                        .AddSingleton(TestSqlServerModelSource.GetFactory(OnModelCreating))
                        .AddSingleton<ILoggerFactory>(new TestSqlLoggerFactory())
                        .BuildServiceProvider();
            }

            public virtual SqlServerTestStore GetTestStore()
            {
                var testStore = SqlServerTestStore.CreateScratch();

                using (var context = CreateContext(testStore))
                {
                    context.Database.EnsureCreated();

                    testStore.ExecuteNonQuery(@"
CREATE TRIGGER TRG_InsertProduct
ON Product
AFTER INSERT AS
BEGIN
	if @@ROWCOUNT = 0
		return
	set nocount on;

    INSERT INTO ProductBackup
    SELECT * FROM INSERTED;
END");

                    testStore.ExecuteNonQuery(@"
CREATE TRIGGER TRG_UpdateProduct
ON Product
AFTER UPDATE AS
BEGIN
	if @@ROWCOUNT = 0
		return
	set nocount on;

    DELETE FROM ProductBackup
    WHERE Id IN(SELECT DELETED.Id FROM DELETED);

    INSERT INTO ProductBackup
    SELECT * FROM INSERTED;
END");

                    testStore.ExecuteNonQuery(@"
CREATE TRIGGER TRG_DeleteProduct
ON Product
AFTER DELETE AS
BEGIN
	if @@ROWCOUNT = 0
		return
	set nocount on;

    DELETE FROM ProductBackup
    WHERE Id IN(SELECT DELETED.Id FROM DELETED);
END");
                }

                return testStore;
            }

            public void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Product>().Property(e => e.Version)
                    .ValueGeneratedOnAddOrUpdate()
                    .IsConcurrencyToken();
                modelBuilder.Entity<ProductBackup>().HasBaseType((Type)null)
                    .Property(e => e.Id).ValueGeneratedNever();
            }

            public TriggersContext CreateContext(SqlServerTestStore testStore)
            {
                var optionsBuilder = new DbContextOptionsBuilder();
                optionsBuilder
                    .EnableSensitiveDataLogging()
                    .UseSqlServer(testStore.Connection);
                
                return new TriggersContext(_serviceProvider, optionsBuilder.Options);
            }
        }

        public class TriggersContext : DbContext
        {
            public TriggersContext(IServiceProvider serviceProvider, DbContextOptions options)
                : base(serviceProvider, options)
            {
            }

            public virtual DbSet<Product> Products { get; set; }
            public virtual DbSet<ProductBackup> ProductBackups { get; set; }
        }

        public class Product
        {
            public virtual int Id { get; set; }
            public virtual byte[] Version { get; set; }
            public virtual string Name { get; set; }
        }

        public class ProductBackup : Product
        {
        }
    }
}
