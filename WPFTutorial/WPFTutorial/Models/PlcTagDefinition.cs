using System.Text.Json.Serialization;
using System.Windows.Media;

namespace WPFTutorial.Models;

public class PlcTagDefinition
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Offset")]
    public string Offset { get; set; } = "0.0";

    [JsonPropertyName("Type")]
    public string Type { get; set; } = "REAL";

    public int DbNumber { get; set; } = 5;

    public int ByteOffset
    {
        get
        {
            var parts = Offset.Split('.');
            return int.Parse(parts[0]);
        }
    }

    public int BitOffset
    {
        get
        {
            var parts = Offset.Split('.');
            return parts.Length > 1 ? int.Parse(parts[1]) : 0;
        }
    }

    public string S7Address => $"DB{DbNumber}.DBD{ByteOffset}";

    public PlcTagDataType PlcType => Type.ToUpperInvariant() switch
    {
        "REAL" => PlcTagDataType.Real,
        "INT" => PlcTagDataType.Int,
        "DINT" => PlcTagDataType.DInt,
        "BOOL" => PlcTagDataType.Bool,
        "WORD" => PlcTagDataType.Word,
        _ => PlcTagDataType.Real,
    };
}

public enum PlcTagDataType
{
    Real,
    Int,
    DInt,
    Bool,
    Word,
}

public class PlcTagValue
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public object? Value { get; set; }
    public string DisplayValue => Value switch
    {
        double d => $"{d:F2}",
        float f => $"{f:F2}",
        int i => $"{i}",
        bool b => b ? "TRUE" : "FALSE",
        _ => Value?.ToString() ?? "---",
    };
    public bool IsConnected { get; set; }
    public string Quality => IsConnected ? "Good" : "Bad";
}
