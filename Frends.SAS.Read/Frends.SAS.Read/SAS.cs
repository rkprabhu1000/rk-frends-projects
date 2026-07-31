using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Frends.SAS.Read.Definitions;
using Frends.SAS.Read.Helpers;
using SasReader;

namespace Frends.SAS.Read;

/// <summary>
/// Task class for reading SAS .sas7bdat data files.
/// </summary>
public static class SAS
{
    /// <summary>
    /// Reads a SAS .sas7bdat data file and converts its contents to structured JSON:
    /// column metadata plus data rows keyed by column name. Handles SAS's internal
    /// RLE/RDC compression and encoding detection transparently.
    ///
    /// [Documentation](https://tasks.frends.com/tasks/CHANNEL/Frends.SAS.Read)
    /// </summary>
    /// <param name="input">Source file location: a file path on the agent, or in-memory bytes.</param>
    /// <param name="options">Column metadata inclusion, date formatting, row cap, encoding override, and error-handling behaviour.</param>
    /// <param name="cancellationToken">A cancellation token provided by the Frends Platform.</param>
    /// <returns>
    /// object {
    ///   bool Success,
    ///   object[] Columns { string Name, string Label, string SasType, string Format, int Length },
    ///   int RowCount,
    ///   object[] Rows,
    ///   object Error { string Message, Exception AdditionalInfo }
    /// }
    /// </returns>
    public static async Task<Result> Read(
        [PropertyTab] Input input,
        [PropertyTab] Options options,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (input.SourceType == SasInputType.File && string.IsNullOrWhiteSpace(input.FilePath))
                throw new ArgumentException("FilePath cannot be empty when SourceType is File.", nameof(input));
            if (input.SourceType == SasInputType.Bytes && (input.Data == null || input.Data.Length == 0))
                throw new ArgumentException("Data cannot be empty when SourceType is Bytes.", nameof(input));

            using Stream stream = input.SourceType == SasInputType.File
                ? new FileStream(input.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true)
                : new MemoryStream(input.Data, writable: false);

            var result = await Task.Run(() => ReadSasStream(stream, options), cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            if (options.ThrowErrorOnFailure)
                throw;

            return new Result
            {
                Success = false,
                Error = new Error
                {
                    Message = string.IsNullOrWhiteSpace(options.ErrorMessageOnFailure)
                        ? ex.Message
                        : options.ErrorMessageOnFailure,
                    AdditionalInfo = ex,
                },
            };
        }
    }

    private static Result ReadSasStream(Stream stream, Options options)
    {
        var outputDateType = options.DateOutputFormat == SasDateOutputFormat.Iso8601String
            ? OutputDateType.JAVA_DATE_LEGACY
            : OutputDateType.SAS_VALUE;

        var encodingOverride = string.IsNullOrWhiteSpace(options.EncodingOverride)
            ? null
            : options.EncodingOverride;

        var reader = new SasFileReaderImpl(stream, encodingOverride, outputDateType);
        var columns = reader.getColumns();
        var convertDates = options.DateOutputFormat == SasDateOutputFormat.Iso8601String;

        var rows = new List<Dictionary<string, object>>();
        object[] rawRow;
        while ((options.MaxRows <= 0 || rows.Count < options.MaxRows) && (rawRow = reader.readNext()) != null)
        {
            var row = new Dictionary<string, object>(columns.Count);
            for (var i = 0; i < columns.Count; i++)
            {
                var formatName = columns[i].getFormat()?.getName();
                row[columns[i].getName()] = SasTemporalHelper.ToJsonValue(rawRow[i], formatName, convertDates);
            }

            rows.Add(row);
        }

        return new Result
        {
            Success = true,
            Columns = options.IncludeColumnMetadata ? BuildColumnInfos(columns) : null,
            RowCount = rows.Count,
            Rows = rows,
        };
    }

    private static List<ColumnInfo> BuildColumnInfos(List<Column> columns)
    {
        var columnInfos = new List<ColumnInfo>(columns.Count);
        foreach (var column in columns)
        {
            columnInfos.Add(new ColumnInfo
            {
                Name = column.getName(),
                Label = column.getLabel() ?? string.Empty,
                SasType = column.getType() == typeof(string) ? "Character" : "Numeric",
                Format = column.getFormat()?.getName() ?? string.Empty,
                Length = column.getLength(),
            });
        }

        return columnInfos;
    }
}
