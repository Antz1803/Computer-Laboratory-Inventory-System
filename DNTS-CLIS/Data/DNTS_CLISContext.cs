using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DNTS_CLIS.Models;
using DNTS_CLIS.Controllers;



namespace DNTS_CLIS.Data
{
    public class DNTS_CLISContext : DbContext
    {
        public DNTS_CLISContext (DbContextOptions<DNTS_CLISContext> options)
            : base(options)
        {
        }

        public DbSet<DNTS_CLIS.Models.TrackRecords> TrackRecords { get; set; } = default!;
        public DbSet<Laboratories> Laboratories { get; set; }
        public DbSet<AssignedLaboratories> AssignedLaboratories { get; set; }
        public DbSet<DeployItem> DeployItems { get; set; }
        public DbSet<DeploymentInfo> DeploymentInfos { get; set; }
        public DbSet<EquipmentDetails> EquipmentDetails { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EquipmentDetails>().HasNoKey();
            base.OnModelCreating(modelBuilder);
        }
        public DbSet<DNTS_CLIS.Models.User> User { get; set; }
    }
}
