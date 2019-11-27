using System;

namespace theori
{
    public struct HybridLabel : IEquatable<HybridLabel>
    {
        public enum Kind
        {
            Text, Number
        }

        public static implicit operator HybridLabel(string text) => new HybridLabel(text);
        public static implicit operator HybridLabel(int number) => new HybridLabel(number);

        public static explicit operator int(HybridLabel label)
        {
            if (label.LabelKind != Kind.Number)
                throw new ArgumentException("Label was not numeric.", nameof(label));
            return label.m_num;
        }

        public static explicit operator string(HybridLabel label)
        {
            if (label.LabelKind != Kind.Text)
                throw new ArgumentException("Label was not textual.", nameof(label));
            return label.m_text!;
        }

        public static bool operator ==(HybridLabel a, HybridLabel b) => a.Equals(b);
        public static bool operator !=(HybridLabel a, HybridLabel b) => !a.Equals(b);

        public static bool operator ==(HybridLabel a, string b) => a.LabelKind == Kind.Text && a.m_text == b;
        public static bool operator !=(HybridLabel a, string b) => a.LabelKind != Kind.Text || a.m_text != b;

        public static bool operator ==(HybridLabel a, int b) => a.LabelKind == Kind.Number && a.m_num == b;
        public static bool operator !=(HybridLabel a, int b) => a.LabelKind != Kind.Number || a.m_num != b;

        private readonly string? m_text;
        private readonly int m_num;

        private readonly int m_hashCode;

        public readonly Kind LabelKind;

        public HybridLabel(string text)
        {
            LabelKind = Kind.Text;
            m_text = text ?? throw new ArgumentNullException(nameof(text));
            m_hashCode = m_text.GetHashCode();

            m_num = 0;
        }

        public HybridLabel(int num)
        {
            LabelKind = Kind.Number;
            m_num = num;
            m_hashCode = num;

            m_text = null;
        }

        public object ToObject() => LabelKind == Kind.Number ? (object)m_num : m_text!;

        public override bool Equals(object obj) => obj is HybridLabel label && Equals(label);
        public bool Equals(HybridLabel that)
        {
            if (LabelKind != that.LabelKind) return false;
            return LabelKind == Kind.Text ? m_text == that.m_text : m_num == that.m_num;
        }

        public override int GetHashCode() => m_hashCode;

        public override string ToString() => LabelKind == Kind.Text ? m_text! : m_num.ToString();
    }
}
