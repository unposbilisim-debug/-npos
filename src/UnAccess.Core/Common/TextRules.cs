using UnAccess.Core.Exceptions;

namespace UnAccess.Core.Common;

public static class TextRules
{
    public static string Required(string? value, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new UnAccessException($"{label} zorunludur.");

        var text = value.Trim();
        if (text.Any(char.IsControl))
            throw new UnAccessException($"{label} geçersiz karakter içeriyor.");
        if (text.Length > maxLength)
            throw new UnAccessException($"{label} en fazla {maxLength} karakter olabilir.");

        return text;
    }

    public static string? Optional(string? value, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        if (text.Any(char.IsControl))
            throw new UnAccessException($"{label} geçersiz karakter içeriyor.");
        if (text.Length > maxLength)
            throw new UnAccessException($"{label} en fazla {maxLength} karakter olabilir.");

        return text;
    }

    public static string Code(string? value, string label) =>
        Required(value, label, FieldLengths.Code);

    public static string Name(string? value, string label) =>
        Required(value, label, FieldLengths.Name);

    public static string PersonName(string? value, string label) =>
        Required(value, label, FieldLengths.PersonName);

    public static string PersonnelNumber(string? value)
    {
        var text = Required(value, "Sicil numarası", FieldLengths.PersonnelNumber);
        if (text.Any(char.IsWhiteSpace))
            throw new UnAccessException("Sicil numarası boşluk içeremez.");
        return text;
    }

    public static string CardNumber(string? value)
    {
        var text = Required(value, "Kart numarası", FieldLengths.CardNumber);
        if (text.Any(char.IsWhiteSpace))
            throw new UnAccessException("Kart numarası boşluk içeremez.");
        return text;
    }

    public static string? Phone(string? value) =>
        Optional(value, "Telefon", FieldLengths.Phone);

    public static string? Email(string? value)
    {
        var text = Optional(value, "E-posta", FieldLengths.Email);
        if (text == null)
            return null;

        var at = text.IndexOf('@');
        if (at <= 0 || at != text.LastIndexOf('@'))
            throw new UnAccessException("E-posta adresi geçersiz.");

        var domain = text[(at + 1)..];
        if (!domain.Contains('.', StringComparison.Ordinal) || text.Contains(' ', StringComparison.Ordinal))
            throw new UnAccessException("E-posta adresi geçersiz.");

        return text;
    }
}
