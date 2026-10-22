using System.Globalization;
using System.Text.RegularExpressions;

namespace SimplexCalc;

public sealed class MainPage : ContentPage
{
    private static readonly Color Ink = Color.FromArgb("#17233C");
    private static readonly Color Muted = Color.FromArgb("#68758B");
    private static readonly Color Accent = Color.FromArgb("#365FE8");
    private static readonly Regex ConstraintPattern = new(
        @"^\s*(.*?)\s*(<=|>=|=)\s*(.*?)\s*$",
        RegexOptions.Compiled);

    private readonly Picker _objectiveTypePicker = new();
    private readonly Entry _objectiveEntry = new();
    private readonly Editor _constraintsEditor = new();
    private readonly Label _resultLabel = new();
    private readonly Editor _stepsEditor = new();
    private readonly Border _resultCard;

    public MainPage()
    {
        Title = "Simplex Calculator";
        BackgroundColor = Color.FromArgb("#F3F5F9");

        _objectiveTypePicker.ItemsSource = new[] { "Maksimalkan (Max)", "Minimalkan (Min)" };
        _objectiveTypePicker.SelectedIndex = 0;
        _objectiveTypePicker.BackgroundColor = Colors.White;
        _objectiveTypePicker.TextColor = Ink;
        _objectiveTypePicker.Title = "Pilih jenis optimasi";

        _objectiveEntry.Text = "3, 5";
        _objectiveEntry.Placeholder = "Contoh: 3, 5";
        _objectiveEntry.Keyboard = Keyboard.Default;
        _objectiveEntry.ClearButtonVisibility = ClearButtonVisibility.WhileEditing;
        StyleInput(_objectiveEntry);

        _constraintsEditor.Text = "1, 0 <= 4" + Environment.NewLine +
            "0, 2 <= 12" + Environment.NewLine +
            "3, 2 <= 18";
        _constraintsEditor.Placeholder = "Satu kendala per baris";
        _constraintsEditor.AutoSize = EditorAutoSizeOption.TextChanges;
        _constraintsEditor.MinimumHeightRequest = 120;
        _constraintsEditor.FontFamily = "monospace";
        StyleInput(_constraintsEditor);

        _resultLabel.FontSize = 18;
        _resultLabel.FontAttributes = FontAttributes.Bold;
        _resultLabel.TextColor = Ink;
        _resultLabel.LineBreakMode = LineBreakMode.WordWrap;

        _resultCard = new Border
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#EAF0FF"),
            Stroke = Color.FromArgb("#D8E2FF"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text = "HASIL OPTIMUM",
                        TextColor = Accent,
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold
                    },
                    _resultLabel
                }
            }
        };

        _stepsEditor.IsReadOnly = true;
        _stepsEditor.Text = "Tableau akan muncul di sini setelah kalkulasi.";
        _stepsEditor.FontFamily = "monospace";
        _stepsEditor.FontSize = 12;
        _stepsEditor.AutoSize = EditorAutoSizeOption.TextChanges;
        _stepsEditor.MinimumHeightRequest = 220;
        _stepsEditor.BackgroundColor = Color.FromArgb("#F8F9FC");
        StyleInput(_stepsEditor);

        var calculateButton = new Button
        {
            Text = "Hitung solusi",
            BackgroundColor = Accent,
            TextColor = Colors.White,
            CornerRadius = 10,
            FontAttributes = FontAttributes.Bold
        };
        calculateButton.Clicked += CalculateButton_Clicked;

        var exampleButton = new Button
        {
            Text = "Muat contoh",
            BackgroundColor = Colors.White,
            TextColor = Ink,
            BorderColor = Color.FromArgb("#D9DFEA"),
            BorderWidth = 1,
            CornerRadius = 10
        };
        exampleButton.Clicked += LoadExampleButton_Clicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 20, 20, 28),
                Spacing = 16,
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Label
                            {
                                Text = "SIMPLEX CALCULATOR",
                                TextColor = Accent,
                                FontSize = 12,
                                FontAttributes = FontAttributes.Bold
                            },
                            new Label
                            {
                                Text = "Optimalkan solusi linear",
                                TextColor = Ink,
                                FontSize = 27,
                                FontAttributes = FontAttributes.Bold
                            },
                            new Label
                            {
                                Text = "Masukkan fungsi tujuan dan kendala untuk melihat nilai optimum serta tableau setiap langkah.",
                                TextColor = Muted,
                                FontSize = 13
                            }
                        }
                    },
                    CreateCard(
                        "1. Fungsi tujuan",
                        new Label { Text = "Jenis optimasi", TextColor = Muted, FontSize = 12 },
                        _objectiveTypePicker,
                        new Label
                        {
                            Text = "Koefisien variabel dipisahkan koma (c1, c2, ...)",
                            TextColor = Muted,
                            FontSize = 12
                        },
                        _objectiveEntry,
                        new Label
                        {
                            Text = "Contoh: 3, 5 berarti Z = 3x1 + 5x2",
                            TextColor = Muted,
                            FontSize = 11
                        }),
                    CreateCard(
                        "2. Kendala",
                        new Label
                        {
                            Text = "Satu kendala per baris: koefisien operator nilai",
                            TextColor = Muted,
                            FontSize = 12
                        },
                        _constraintsEditor,
                        new Label
                        {
                            Text = "Operator: <=, >=, =. Semua variabel x1, x2, ... diasumsikan >= 0. Desimal gunakan titik.",
                            TextColor = Muted,
                            FontSize = 11
                        }),
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children = { calculateButton, exampleButton }
                    },
                    _resultCard,
                    CreateCard(
                        "Langkah penyelesaian",
                        new Label
                        {
                            Text = "Metode two-phase digunakan untuk kendala yang memerlukan variabel artifisial.",
                            TextColor = Muted,
                            FontSize = 12
                        },
                        _stepsEditor)
                }
            }
        };
    }

    private async void CalculateButton_Clicked(object? sender, EventArgs e)
    {
        _resultCard.IsVisible = false;

        try
        {
            var objective = ParseNumbers(_objectiveEntry.Text ?? string.Empty, "Fungsi tujuan");
            var constraints = ParseConstraints(_constraintsEditor.Text ?? string.Empty, objective.Length);
            var maximize = _objectiveTypePicker.SelectedIndex == 0;
            var result = SimplexSolver.Solve(objective, constraints, maximize);

            _resultLabel.Text = $"Z = {FormatValue(result.ObjectiveValue)}" +
                Environment.NewLine +
                string.Join("    ", result.VariableValues.Select(
                    (value, index) => $"x{index + 1} = {FormatValue(value)}"));
            _stepsEditor.Text = string.Join(
                Environment.NewLine + Environment.NewLine,
                result.Steps);
            _resultCard.IsVisible = true;
        }
        catch (FormatException exception)
        {
            await DisplayAlert("Input tidak valid", exception.Message, "OK");
        }
        catch (InvalidOperationException exception)
        {
            await DisplayAlert("Tidak dapat menghitung", exception.Message, "OK");
        }
    }

    private void LoadExampleButton_Clicked(object? sender, EventArgs e)
    {
        _objectiveTypePicker.SelectedIndex = 0;
        _objectiveEntry.Text = "3, 5";
        _constraintsEditor.Text = "1, 0 <= 4" + Environment.NewLine +
            "0, 2 <= 12" + Environment.NewLine +
            "3, 2 <= 18";
        _resultCard.IsVisible = false;
        _stepsEditor.Text = "Contoh dimuat. Pilih Hitung solusi untuk melihat hasilnya.";
    }

    private static View CreateCard(string title, params View[] content)
    {
        var children = new List<View>
        {
            new Label
            {
                Text = title,
                TextColor = Ink,
                FontSize = 17,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            }
        };
        children.AddRange(content);

        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#E3E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children = children
            }
        };
    }

    private static void StyleInput(VisualElement input)
    {
        input.BackgroundColor = Colors.White;
        input.Margin = new Thickness(0, 2);
    }

    private static List<LinearConstraint> ParseConstraints(string text, int variableCount)
    {
        var constraints = new List<LinearConstraint>();
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var match = ConstraintPattern.Match(line);
            if (!match.Success)
            {
                throw new FormatException(
                    $"Format kendala baris {index + 1} tidak valid. Contoh: 1, 2 <= 10.");
            }

            var coefficients = ParseNumbers(match.Groups[1].Value, $"Kendala baris {index + 1}");
            if (coefficients.Length != variableCount)
            {
                throw new FormatException(
                    $"Kendala baris {index + 1} harus memiliki {variableCount} koefisien.");
            }

            var rightHandSide = ParseNumber(match.Groups[3].Value, $"Nilai kanan baris {index + 1}");
            var relation = match.Groups[2].Value switch
            {
                "<=" => ConstraintRelation.LessOrEqual,
                ">=" => ConstraintRelation.GreaterOrEqual,
                "=" => ConstraintRelation.Equal,
                _ => throw new FormatException($"Operator kendala baris {index + 1} tidak valid.")
            };

            constraints.Add(new LinearConstraint(coefficients, relation, rightHandSide));
        }

        if (constraints.Count == 0)
        {
            throw new FormatException("Masukkan setidaknya satu kendala.");
        }

        return constraints;
    }

    private static double[] ParseNumbers(string text, string fieldName)
    {
        var parts = text.Split(',');
        if (parts.Length == 0 || parts.Any(part => string.IsNullOrWhiteSpace(part)))
        {
            throw new FormatException($"{fieldName}: masukkan koefisien yang dipisahkan koma.");
        }

        return parts.Select((part, index) =>
            ParseNumber(part, $"{fieldName}, koefisien {index + 1}")).ToArray();
    }

    private static double ParseNumber(string text, string fieldName)
    {
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            !double.IsFinite(value))
        {
            throw new FormatException($"{fieldName} harus berupa angka yang valid.");
        }

        return value;
    }

    private static string FormatValue(double value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);
}
