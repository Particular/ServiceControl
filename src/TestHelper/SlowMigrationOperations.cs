namespace TestHelper;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Particular.Approvals;

public static class SlowMigrationOperations
{
    const string ChangedMessage =
        "The migration steps that can block writes on a large table do not match the approved file. Index, key, foreign key, check constraint, column and raw SQL changes can block writes for a long time during an upgrade. " +
        "If the diff shows a changed step, record it in the upgrade guide of the next release. If the step is in an existing migration, add a new migration instead. " +
        "Then approve the new list. See docs/testing.md.";

    /// <summary>
    /// Compares the slow steps in the migrations of <paramref name="context"/> with the approved file for the calling test.
    /// The slow steps are index, primary key and unique constraint changes, new foreign keys and check constraints, altered columns, raw SQL,
    /// and new columns that are NOT NULL with a default or are stored computed columns.
    /// Other steps, such as renames and drops of columns, tables and constraints, are not listed.
    /// Steps on a table that the same migration creates are left out, because that table starts empty. Raw SQL is always listed.
    /// When the list differs, it throws an exception that says what to check before approving the new list, with the approval failure as the inner exception.
    /// </summary>
    public static void Verify(DbContext context, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
    {
        var steps = Describe(context);

        try
        {
            Approver.Verify(steps, callerFilePath: callerFilePath, callerMemberName: callerMemberName);
        }
        catch (Exception e)
        {
            throw new Exception(ChangedMessage, e);
        }
    }

    static string Describe(DbContext context)
    {
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var text = new StringBuilder();

        foreach (var (id, type) in migrationsAssembly.Migrations.OrderBy(migration => migration.Key, StringComparer.Ordinal))
        {
            var operations = migrationsAssembly.CreateMigration(type, context.Database.ProviderName).UpOperations;
            var newTables = operations.OfType<CreateTableOperation>().Select(table => table.Name).ToHashSet(StringComparer.Ordinal);
            var steps = operations.Select(operation => Describe(operation, newTables)).OfType<string>().ToList();

            if (steps.Count == 0)
            {
                continue;
            }

            text.AppendLine(id);
            foreach (var step in steps)
            {
                text.AppendLine(Indent(step));
            }
        }

        return text.ToString();
    }

    static string Describe(MigrationOperation operation, HashSet<string> newTables) => operation switch
    {
        SqlOperation sql => $"Sql{Environment.NewLine}{Indent(sql.Sql)}",
        ITableMigrationOperation table when newTables.Contains(table.Table) => null,
        CreateIndexOperation index => $"CreateIndex {index.Name} on {index.Table} ({IndexColumns(index)}){(index.IsUnique ? " unique" : "")}{(index.Filter is null ? "" : $" where {index.Filter}")}{Annotations(index)}",
        DropIndexOperation index => $"DropIndex {index.Name} on {index.Table}",
        AddPrimaryKeyOperation key => $"AddPrimaryKey {key.Name} on {key.Table} ({string.Join(", ", key.Columns)}){Annotations(key)}",
        DropPrimaryKeyOperation key => $"DropPrimaryKey {key.Name} on {key.Table}",
        AddUniqueConstraintOperation constraint => $"AddUniqueConstraint {constraint.Name} on {constraint.Table} ({string.Join(", ", constraint.Columns)}){Annotations(constraint)}",
        DropUniqueConstraintOperation constraint => $"DropUniqueConstraint {constraint.Name} on {constraint.Table}",
        AddCheckConstraintOperation constraint => $"AddCheckConstraint {constraint.Name} on {constraint.Table} ({constraint.Sql})",
        AddForeignKeyOperation key => $"AddForeignKey {key.Name} on {key.Table} ({string.Join(", ", key.Columns)}) references {key.PrincipalTable} ({string.Join(", ", key.PrincipalColumns ?? [])})",
        AlterColumnOperation column => $"AlterColumn {column.Table}.{column.Name} {ColumnType(column.OldColumn)} -> {ColumnType(column)}",
        AddColumnOperation column when CanRewriteEveryRow(column) => $"AddColumn {column.Table}.{column.Name} {ColumnType(column)}{ColumnValue(column)}",
        _ => null
    };

    static bool CanRewriteEveryRow(AddColumnOperation column) =>
        (!column.IsNullable && (column.DefaultValue is not null || column.DefaultValueSql is not null)) || (column.ComputedColumnSql is not null && column.IsStored == true);

    static string ColumnValue(ColumnOperation column) =>
        column.ComputedColumnSql is not null ? $" computed {column.ComputedColumnSql}"
        : column.DefaultValueSql is not null ? $" default {column.DefaultValueSql}"
        : $" default {Convert.ToString(column.DefaultValue, CultureInfo.InvariantCulture)}";

    // An empty IsDescending array means every column is descending.
    static string IndexColumns(CreateIndexOperation index) => string.Join(", ", index.Columns.Select((column, i) =>
        index.IsDescending is { } descending && (descending.Length == 0 || descending[i]) ? $"{column} desc" : column));

    static string ColumnType(ColumnOperation column) => $"{column.ColumnType ?? column.ClrType.Name} {(column.IsNullable ? "null" : "not null")}";

    static string Annotations(MigrationOperation operation) => string.Concat(operation.GetAnnotations().Select(annotation =>
        $" {annotation.Name}={(annotation.Value is IEnumerable<string> values ? $"[{string.Join(", ", values)}]" : annotation.Value)}"));

    static string Indent(string text) => string.Join(Environment.NewLine, text.Split('\n').Select(line => line.TrimEnd('\r')).Select(line => line.Length == 0 ? line : "    " + line));
}
