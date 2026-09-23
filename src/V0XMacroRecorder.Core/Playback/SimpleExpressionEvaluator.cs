using System.Globalization;

namespace V0XMacroRecorder.Core.Playback;

/// <summary>
/// Petit évaluateur d'expression arithmétique (+ - * / et parenthèses, nombres décimaux) pour la commande Variable
/// en mode Calculer, une fois les jetons {var:nom} déjà expansés par <see cref="TokenExpander"/>. Ne lève jamais :
/// une expression malformée ou une division par zéro renvoie 0, cohérent avec le reste du moteur (jamais de
/// commande utilisateur mal formée qui fait planter toute la lecture).
/// </summary>
public static class SimpleExpressionEvaluator
{
    public static double Evaluate(string expression)
    {
        try
        {
            var parser = new Parser(expression.Trim());
            var result = parser.ParseExpression();
            return parser.AtEnd ? result : 0;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or DivideByZeroException)
        {
            return 0;
        }
    }

    private sealed class Parser(string text)
    {
        private int _pos;

        public bool AtEnd => _pos >= text.Length;

        public double ParseExpression()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Peek() == '+') { _pos++; value += ParseTerm(); }
                else if (Peek() == '-') { _pos++; value -= ParseTerm(); }
                else { return value; }
            }
        }

        private double ParseTerm()
        {
            var value = ParseFactor();
            while (true)
            {
                SkipWhitespace();
                if (Peek() == '*') { _pos++; value *= ParseFactor(); }
                else if (Peek() == '/')
                {
                    _pos++;
                    var divisor = ParseFactor();
                    value = divisor == 0 ? 0 : value / divisor;
                }
                else { return value; }
            }
        }

        private double ParseFactor()
        {
            SkipWhitespace();
            if (Peek() == '-')
            {
                _pos++;
                return -ParseFactor();
            }

            if (Peek() == '(')
            {
                _pos++;
                var value = ParseExpression();
                SkipWhitespace();
                if (Peek() == ')')
                {
                    _pos++;
                }

                return value;
            }

            var start = _pos;
            while (_pos < text.Length && (char.IsAsciiDigit(text[_pos]) || text[_pos] == '.'))
            {
                _pos++;
            }

            if (_pos == start)
            {
                throw new FormatException("Nombre attendu.");
            }

            return double.Parse(text[start.._pos], CultureInfo.InvariantCulture);
        }

        private char Peek() => _pos < text.Length ? text[_pos] : '\0';

        private void SkipWhitespace()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
            {
                _pos++;
            }
        }
    }
}
