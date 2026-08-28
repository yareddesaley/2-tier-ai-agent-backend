using System;
using AiTier2Support.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace AiTier2Support.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.AgentAction", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("ActionType")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<double>("Confidence")
                        .HasColumnType("double precision");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime?>("ExecutedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("IncidentId")
                        .HasColumnType("uuid");

                    b.Property<string>("ParametersJson")
                        .HasColumnType("text");

                    b.Property<string>("Reason")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ResultJson")
                        .HasColumnType("text");

                    b.Property<int>("RiskLevel")
                        .HasColumnType("integer");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("VerificationJson")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("IncidentId");

                    b.ToTable("AgentActions");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.AgentMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AgentRunId")
                        .HasColumnType("uuid");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Sequence")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AgentRunId");

                    b.ToTable("AgentMessages");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.AgentRun", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("FailureReason")
                        .HasColumnType("text");

                    b.Property<Guid>("IncidentId")
                        .HasColumnType("uuid");

                    b.Property<int>("IterationCount")
                        .HasColumnType("integer");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("IncidentId");

                    b.ToTable("AgentRuns");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.ApprovalRequest", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AgentActionId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime?>("ReviewedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ReviewedBy")
                        .HasColumnType("text");

                    b.Property<string>("ReviewNotes")
                        .HasColumnType("text");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AgentActionId")
                        .IsUnique();

                    b.HasIndex("Status");

                    b.ToTable("ApprovalRequests");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.Evidence", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("IncidentId")
                        .HasColumnType("uuid");

                    b.Property<string>("Observation")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("ObservedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Source")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Tool")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("IncidentId");

                    b.ToTable("Evidence");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.Incident", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<double?>("Confidence")
                        .HasColumnType("double precision");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("DiagnosisJson")
                        .HasColumnType("text");

                    b.Property<string>("EscalationNextStep")
                        .HasColumnType("text");

                    b.Property<string>("EscalationReason")
                        .HasColumnType("text");

                    b.Property<string>("RecommendedAction")
                        .HasColumnType("text");

                    b.Property<int?>("RiskLevel")
                        .HasColumnType("integer");

                    b.Property<string>("RootCause")
                        .HasColumnType("text");

                    b.Property<string>("ScenarioId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Severity")
                        .HasColumnType("integer");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("Status");

                    b.ToTable("Incidents");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.IncidentReport", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime>("GeneratedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid>("IncidentId")
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("IncidentId")
                        .IsUnique();

                    b.ToTable("IncidentReports");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.ToolExecution", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AgentRunId")
                        .HasColumnType("uuid");

                    b.Property<string>("ArgumentsJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("text");

                    b.Property<string>("ResultJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Sequence")
                        .HasColumnType("integer");

                    b.Property<bool>("Success")
                        .HasColumnType("boolean");

                    b.Property<string>("ToolName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AgentRunId");

                    b.ToTable("ToolExecutions");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.AgentAction", b =>
                {
                    b.HasOne("AiTier2Support.Domain.Incidents.Incident", "Incident")
                        .WithMany("Actions")
                        .HasForeignKey("IncidentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Incident");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.AgentMessage", b =>
                {
                    b.HasOne("AiTier2Support.Domain.Incidents.AgentRun", "AgentRun")
                        .WithMany("Messages")
                        .HasForeignKey("AgentRunId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("AgentRun");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.AgentRun", b =>
                {
                    b.HasOne("AiTier2Support.Domain.Incidents.Incident", "Incident")
                        .WithMany("AgentRuns")
                        .HasForeignKey("IncidentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Incident");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.ApprovalRequest", b =>
                {
                    b.HasOne("AiTier2Support.Domain.Incidents.AgentAction", "AgentAction")
                        .WithOne("ApprovalRequest")
                        .HasForeignKey("AiTier2Support.Domain.Incidents.ApprovalRequest", "AgentActionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("AgentAction");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.Evidence", b =>
                {
                    b.HasOne("AiTier2Support.Domain.Incidents.Incident", "Incident")
                        .WithMany("Evidence")
                        .HasForeignKey("IncidentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Incident");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.IncidentReport", b =>
                {
                    b.HasOne("AiTier2Support.Domain.Incidents.Incident", "Incident")
                        .WithOne("Report")
                        .HasForeignKey("AiTier2Support.Domain.Incidents.IncidentReport", "IncidentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Incident");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.ToolExecution", b =>
                {
                    b.HasOne("AiTier2Support.Domain.Incidents.AgentRun", "AgentRun")
                        .WithMany("ToolExecutions")
                        .HasForeignKey("AgentRunId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("AgentRun");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.AgentAction", b =>
                {
                    b.Navigation("ApprovalRequest");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.AgentRun", b =>
                {
                    b.Navigation("Messages");

                    b.Navigation("ToolExecutions");
                });

            modelBuilder.Entity("AiTier2Support.Domain.Incidents.Incident", b =>
                {
                    b.Navigation("Actions");

                    b.Navigation("AgentRuns");

                    b.Navigation("Evidence");

                    b.Navigation("Report");
                });
#pragma warning restore 612, 618
        }
    }
}
