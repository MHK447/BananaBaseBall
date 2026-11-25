using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>Health and attack at each hero level, indexed once from the bundled balance sheet.</summary>
    public static class ColosseumUnitInfo
    {
        public const string TableName = "UnitInfo";
        static readonly string[] Columns = { "unit_key", "name", "health", "damage", "health_per_level", "damage_per_level" };
        static Dictionary<WeaponKind, Stats[]> levels;

        public readonly struct Stats
        {
            public readonly int Health, Attack;

            internal Stats(int health, int attack)
            {
                Health = health;
                Attack = attack;
            }
        }

        public static Stats For(WeaponKind kind, int level)
        {
            if (levels == null)
            {
                var bundled = Resources.Load<TextAsset>(ColosseumEventTable.Folder + "/" + TableName);
                if (!TryApply(bundled != null ? bundled.text : null, out string error))
                    throw new InvalidOperationException("Bundled UnitInfo: " + error);
            }
            return levels[kind][Mathf.Clamp(level, 1, ColosseumRoster.MaxLevel)];
        }

        /// <summary>Installs a complete sheet in one swap. An invalid edit leaves the last good table active.</summary>
        public static bool TryApply(string csv, out string error)
        {
            try
            {
                levels = Parse(csv);
                error = null;
                return true;
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        static Dictionary<WeaponKind, Stats[]> Parse(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) throw new FormatException("UnitInfo is empty.");
            var rows = ColosseumEventTable.Cells(csv);
            if (rows.Count == 0) throw new FormatException("Missing UnitInfo header.");
            var indices = new int[Columns.Length];
            for (int i = 0; i < Columns.Length; i++)
            {
                string column = Columns[i];
                int index = Array.FindIndex(rows[0], cell => cell.Trim() == column);
                if (index < 0 || index != Array.FindLastIndex(rows[0], cell => cell.Trim() == column))
                    throw new FormatException("Expected one column named " + column + ".");
                indices[i] = index;
            }

            var table = new Dictionary<WeaponKind, Stats[]>();
            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                if (Array.TrueForAll(row, string.IsNullOrWhiteSpace)) continue;
                string Cell(int column) => indices[column] < row.Length ? row[indices[column]].Trim() : "";
                string key = Cell(0);
                var hero = Array.Find(ColosseumRoster.Heroes, entry => entry.Key == key);
                if (hero == null) throw new FormatException("Unknown unit_key '" + key + "' in row " + (r + 1) + ".");
                if (table.ContainsKey(hero.Weapon)) throw new FormatException("Duplicate unit_key '" + key + "'.");
                if (Cell(1).Length == 0) throw new FormatException("Missing name for " + key + ".");
                double Number(int column)
                {
                    if (double.TryParse(Cell(column), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                        && value > 0 && value <= int.MaxValue) return value;
                    throw new FormatException(key + ": invalid " + Columns[column] + ".");
                }
                table.Add(hero.Weapon, BuildLevels(key, Number(2), Number(3), Number(4), Number(5)));
            }
            foreach (var hero in ColosseumRoster.Heroes)
                if (!table.ContainsKey(hero.Weapon)) throw new FormatException("Missing unit_key '" + hero.Key + "'.");

            // Hammer is a non-collectible legacy fighter. The collectible Orc uses the key "hammer" and WeaponKind.Slam.
            table.Add(WeaponKind.Hammer, BuildLevels("legacy hammer", 120, 46, 12, 4.6));
            return table;
        }

        static Stats[] BuildLevels(string key, double health, double damage, double healthStep, double damageStep)
        {
            var result = new Stats[ColosseumRoster.MaxLevel + 1];
            for (int level = 1; level < result.Length; level++)
            {
                // Match Unity's round-to-nearest-even for both the card preview and combat.
                double hp = Math.Round(health + (level - 1) * healthStep);
                double atk = Math.Round(damage + (level - 1) * damageStep);
                if (hp <= result[level - 1].Health || atk <= result[level - 1].Attack || hp > int.MaxValue || atk > int.MaxValue)
                    throw new FormatException(key + ": health and damage must increase at level " + level + " within the integer range.");
                result[level] = new Stats((int)hp, (int)atk);
            }
            return result;
        }
    }
}
