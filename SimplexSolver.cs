using System.Globalization;
using System.Text;

namespace SimplexCalc;

internal enum ConstraintRelation
{
    LessOrEqual,
    GreaterOrEqual,
    Equal
}

internal sealed record LinearConstraint(
    double[] Coefficients,
    ConstraintRelation Relation,
    double RightHandSide);

internal sealed record SimplexResult(double ObjectiveValue, double[] VariableValues, IReadOnlyList<string> Steps);

internal static class SimplexSolver
{
    private const double Epsilon = 1e-9;
    private const int MaximumIterations = 10_000;

    public static SimplexResult Solve(
        double[] objective,
        IReadOnlyList<LinearConstraint> constraints,
        bool maximize)
    {
        if (objective.Length == 0)
        {
            throw new InvalidOperationException("Masukkan setidaknya satu variabel.");
        }

        if (constraints.Count == 0)
        {
            throw new InvalidOperationException("Masukkan setidaknya satu kendala.");
        }

        var variableCount = objective.Length;
        var rows = new List<double[]>();
        var basis = new List<int>();
        var columnNames = Enumerable.Range(1, variableCount)
            .Select(index => $"x{index}")
            .ToList();
        var artificialColumns = new List<int>();

        foreach (var constraint in constraints)
        {
            if (constraint.Coefficients.Length != variableCount)
            {
                throw new InvalidOperationException("Jumlah koefisien variabel pada kendala tidak konsisten.");
            }

            var coefficients = (double[])constraint.Coefficients.Clone();
            var rightHandSide = constraint.RightHandSide;
            var relation = constraint.Relation;

            if (rightHandSide < 0)
            {
                for (var index = 0; index < coefficients.Length; index++)
                {
                    coefficients[index] = -coefficients[index];
                }

                rightHandSide = -rightHandSide;
                relation = relation switch
                {
                    ConstraintRelation.LessOrEqual => ConstraintRelation.GreaterOrEqual,
                    ConstraintRelation.GreaterOrEqual => ConstraintRelation.LessOrEqual,
                    _ => ConstraintRelation.Equal
                };
            }

            rows.Add(coefficients.Concat(new[] { rightHandSide }).ToArray());
            if (relation == ConstraintRelation.LessOrEqual)
            {
                AddColumn("s", columnNames, rows, basis, +1);
            }
            else if (relation == ConstraintRelation.GreaterOrEqual)
            {
                AddColumn("u", columnNames, rows, basis, -1);
                AddArtificialColumn(columnNames, rows, basis, artificialColumns);
            }
            else
            {
                AddArtificialColumn(columnNames, rows, basis, artificialColumns);
            }
        }

        var tableau = new Tableau(rows, basis, columnNames);
        var steps = new List<string>();

        if (artificialColumns.Count > 0)
        {
            var phaseOneCosts = new double[tableau.ColumnCount];
            foreach (var column in artificialColumns)
            {
                phaseOneCosts[column] = -1;
            }

            Optimize(tableau, phaseOneCosts, "Fase I", steps);
            var phaseOneValue = GetObjectiveRow(tableau, phaseOneCosts)[tableau.ColumnCount];
            if (phaseOneValue < -Epsilon)
            {
                throw new InvalidOperationException("Model tidak memiliki solusi yang layak (infeasible).");
            }

            RemoveArtificialVariables(tableau, artificialColumns);
        }

        var phaseTwoCosts = new double[tableau.ColumnCount];
        for (var index = 0; index < variableCount; index++)
        {
            phaseTwoCosts[index] = maximize ? objective[index] : -objective[index];
        }

        Optimize(tableau, phaseTwoCosts, artificialColumns.Count > 0 ? "Fase II" : "Simplex", steps);

        var variableValues = new double[variableCount];
        for (var row = 0; row < tableau.Rows.Count; row++)
        {
            var basicColumn = tableau.Basis[row];
            if (basicColumn < variableCount)
            {
                variableValues[basicColumn] = NormalizeZero(tableau.Rows[row][tableau.ColumnCount]);
            }
        }

        var objectiveValue = objective
            .Select((coefficient, index) => coefficient * variableValues[index])
            .Sum();

        return new SimplexResult(objectiveValue, variableValues, steps);
    }

    private static void AddColumn(
        string prefix,
        List<string> columnNames,
        List<double[]> rows,
        List<int> basis,
        double basicValue)
    {
        var newColumn = columnNames.Count;
        columnNames.Add($"{prefix}{columnNames.Count(name => name.StartsWith(prefix, StringComparison.Ordinal)) + 1}");

        for (var row = 0; row < rows.Count; row++)
        {
            var expanded = new double[columnNames.Count + 1];
            Array.Copy(rows[row], expanded, rows[row].Length - 1);
            expanded[^1] = rows[row][^1];
            rows[row] = expanded;
        }

        var currentRow = rows.Count - 1;
        rows[currentRow][newColumn] = basicValue;
        if (basicValue > 0)
        {
            basis.Add(newColumn);
        }
    }

    private static void AddArtificialColumn(
        List<string> columnNames,
        List<double[]> rows,
        List<int> basis,
        List<int> artificialColumns)
    {
        var newColumn = columnNames.Count;
        columnNames.Add($"a{artificialColumns.Count + 1}");

        for (var row = 0; row < rows.Count; row++)
        {
            var expanded = new double[columnNames.Count + 1];
            Array.Copy(rows[row], expanded, rows[row].Length - 1);
            expanded[^1] = rows[row][^1];
            rows[row] = expanded;
        }

        rows[^1][newColumn] = 1;
        basis.Add(newColumn);
        artificialColumns.Add(newColumn);
    }

    private static void Optimize(Tableau tableau, double[] costs, string phase, List<string> steps)
    {
        steps.Add(FormatTableau(tableau, costs, $"{phase} — tableau awal"));

        for (var iteration = 0; iteration < MaximumIterations; iteration++)
        {
            var objectiveRow = GetObjectiveRow(tableau, costs);
            var enteringColumn = -1;

            for (var column = 0; column < tableau.ColumnCount; column++)
            {
                if (objectiveRow[column] < -Epsilon)
                {
                    enteringColumn = column;
                    break;
                }
            }

            if (enteringColumn < 0)
            {
                return;
            }

            var leavingRow = -1;
            var minimumRatio = double.PositiveInfinity;
            for (var row = 0; row < tableau.Rows.Count; row++)
            {
                var pivotCoefficient = tableau.Rows[row][enteringColumn];
                if (pivotCoefficient <= Epsilon)
                {
                    continue;
                }

                var ratio = tableau.Rows[row][tableau.ColumnCount] / pivotCoefficient;
                if (ratio < minimumRatio - Epsilon ||
                    (Math.Abs(ratio - minimumRatio) <= Epsilon &&
                     (leavingRow < 0 || tableau.Basis[row] < tableau.Basis[leavingRow])))
                {
                    minimumRatio = ratio;
                    leavingRow = row;
                }
            }

            if (leavingRow < 0)
            {
                throw new InvalidOperationException("Model tidak terbatas (unbounded); nilai optimum tidak terhingga.");
            }

            var leavingVariable = tableau.ColumnNames[tableau.Basis[leavingRow]];
            var enteringVariable = tableau.ColumnNames[enteringColumn];
            Pivot(tableau, leavingRow, enteringColumn);
            steps.Add(FormatTableau(
                tableau,
                costs,
                $"{phase} — iterasi {iteration + 1}: {enteringVariable} masuk, {leavingVariable} keluar"));
        }

        throw new InvalidOperationException("Perhitungan melewati batas iterasi. Periksa kembali model.");
    }

    private static void Pivot(Tableau tableau, int pivotRow, int pivotColumn)
    {
        var pivotValue = tableau.Rows[pivotRow][pivotColumn];
        for (var column = 0; column <= tableau.ColumnCount; column++)
        {
            tableau.Rows[pivotRow][column] /= pivotValue;
        }

        for (var row = 0; row < tableau.Rows.Count; row++)
        {
            if (row == pivotRow)
            {
                continue;
            }

            var factor = tableau.Rows[row][pivotColumn];
            if (Math.Abs(factor) <= Epsilon)
            {
                continue;
            }

            for (var column = 0; column <= tableau.ColumnCount; column++)
            {
                tableau.Rows[row][column] -= factor * tableau.Rows[pivotRow][column];
                if (Math.Abs(tableau.Rows[row][column]) <= Epsilon)
                {
                    tableau.Rows[row][column] = 0;
                }
            }
        }

        tableau.Basis[pivotRow] = pivotColumn;
    }

    private static void RemoveArtificialVariables(Tableau tableau, List<int> artificialColumns)
    {
        var artificialSet = artificialColumns.ToHashSet();
        var row = 0;

        while (row < tableau.Rows.Count)
        {
            if (!artificialSet.Contains(tableau.Basis[row]))
            {
                row++;
                continue;
            }

            var replacementColumn = -1;
            for (var column = 0; column < tableau.ColumnCount; column++)
            {
                if (!artificialSet.Contains(column) &&
                    !tableau.Basis.Contains(column) &&
                    Math.Abs(tableau.Rows[row][column]) > Epsilon)
                {
                    replacementColumn = column;
                    break;
                }
            }

            if (replacementColumn >= 0)
            {
                Pivot(tableau, row, replacementColumn);
                row++;
            }
            else if (Math.Abs(tableau.Rows[row][tableau.ColumnCount]) <= Epsilon)
            {
                tableau.Rows.RemoveAt(row);
                tableau.Basis.RemoveAt(row);
            }
            else
            {
                throw new InvalidOperationException("Model tidak memiliki solusi yang layak (infeasible).");
            }
        }

        var keptColumns = Enumerable.Range(0, tableau.ColumnCount)
            .Where(column => !artificialSet.Contains(column))
            .ToArray();
        var remap = new int[tableau.ColumnCount];
        for (var newColumn = 0; newColumn < keptColumns.Length; newColumn++)
        {
            remap[keptColumns[newColumn]] = newColumn;
        }

        for (var index = 0; index < tableau.Rows.Count; index++)
        {
            var reduced = new double[keptColumns.Length + 1];
            for (var column = 0; column < keptColumns.Length; column++)
            {
                reduced[column] = tableau.Rows[index][keptColumns[column]];
            }

            reduced[^1] = tableau.Rows[index][tableau.ColumnCount];
            tableau.Rows[index] = reduced;
            tableau.Basis[index] = remap[tableau.Basis[index]];
        }

        tableau.ColumnNames = keptColumns.Select(column => tableau.ColumnNames[column]).ToList();
    }

    private static double[] GetObjectiveRow(Tableau tableau, double[] costs)
    {
        var objectiveRow = new double[tableau.ColumnCount + 1];
        for (var row = 0; row < tableau.Rows.Count; row++)
        {
            var basicCost = costs[tableau.Basis[row]];
            if (Math.Abs(basicCost) <= Epsilon)
            {
                continue;
            }

            for (var column = 0; column <= tableau.ColumnCount; column++)
            {
                objectiveRow[column] += basicCost * tableau.Rows[row][column];
            }
        }

        for (var column = 0; column < tableau.ColumnCount; column++)
        {
            objectiveRow[column] -= costs[column];
        }

        return objectiveRow;
    }

    private static string FormatTableau(Tableau tableau, double[] costs, string title)
    {
        var objectiveRow = GetObjectiveRow(tableau, costs);
        var builder = new StringBuilder(title).AppendLine();
        builder.Append("Basis".PadRight(9));
        foreach (var columnName in tableau.ColumnNames)
        {
            builder.Append(columnName.PadLeft(11));
        }

        builder.AppendLine("        RHS");
        builder.AppendLine(new string('-', 9 + (tableau.ColumnCount + 1) * 11));

        for (var row = 0; row < tableau.Rows.Count; row++)
        {
            builder.Append(tableau.ColumnNames[tableau.Basis[row]].PadRight(9));
            for (var column = 0; column <= tableau.ColumnCount; column++)
            {
                builder.Append(FormatNumber(tableau.Rows[row][column]).PadLeft(11));
            }

            builder.AppendLine();
        }

        builder.Append("Z".PadRight(9));
        foreach (var value in objectiveRow)
        {
            builder.Append(FormatNumber(value).PadLeft(11));
        }

        return builder.ToString();
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value) <= Epsilon)
        {
            value = 0;
        }

        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static double NormalizeZero(double value) => Math.Abs(value) <= Epsilon ? 0 : value;

    private sealed class Tableau(List<double[]> rows, List<int> basis, List<string> columnNames)
    {
        public List<double[]> Rows { get; } = rows;
        public List<int> Basis { get; } = basis;
        public List<string> ColumnNames { get; set; } = columnNames;
        public int ColumnCount => ColumnNames.Count;
    }
}
