namespace CSVtoABA
{
    public class Program
    {
        private static bool WriteError(string message)
        {
            Console.WriteLine(message);
            return false;
        }
        private static bool Validate(string csvPath)
        {
            using (var sr = new StreamReader(csvPath))
            {
                // validate header columns
                string? header = sr.ReadLine();
                if (header is null)
                    return WriteError("Error: Empty file.");

                string[] headerParts = header.Split(',');
                if (headerParts.Length != 3)
                    return WriteError("Error: Header must have at exactly 3 columns.");

                if (!headerParts[0].Equals("id", StringComparison.OrdinalIgnoreCase))
                    return WriteError("Error: First header column must be id/Id/ID/etc.");

                if (!headerParts[1].StartsWith("created", StringComparison.OrdinalIgnoreCase))
                    return WriteError("Error: Second header column must start with 'created'.");

                if (!headerParts[2].StartsWith("closed", StringComparison.OrdinalIgnoreCase))
                    return WriteError("Error: Third header column must start with 'created'.");

                // validate first data line, to make sure the types are correct
                string? line = sr.ReadLine();
                if (line is null)
                    return WriteError("Error: Missing data line after header.");

                string[] parts = line.Split(',');
                if (parts.Length != 3)
                    return WriteError("Error: must be 3 values, even if the last one is blank");

                if (!int.TryParse(parts[0], out _))
                    return WriteError("Error: First field must be the ID, an int.");

                if (!DateTime.TryParse(parts[1], out _))
                    return WriteError("Error: Second field must be a date.");

                if (parts[2].Length != 0)
                    if (!DateTime.TryParse(parts[2], out _))
                        return WriteError("Error: Third field must be a date or blank.");

                return true;
            }
        }

        private static Dictionary<DateTime, (double sumAges, int count)> ComputeABA(string csvPath, DateTime analysisEnd)
        {
            var map = new Dictionary<DateTime, (double sumAges, int count)>();
            using (var sr = new StreamReader(csvPath))
            {
                sr.ReadLine(); // skip header (already validated)

                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split(',');
                    if (parts.Length != 3)
                    {
                        WriteError($"malformed line {line}; continuing anyway");
                        continue;
                    }

                    int id = int.Parse(parts[0]);
                    DateTime created = DateTime.Parse(parts[1]);

                    DateTime? closed = null;
                    if (parts[2].Length != 0)
                        closed = DateTime.Parse(parts[2]);
                    DateTime end = closed ?? analysisEnd;
                                        
                    for (DateTime t = created.Date; t <= end.Date; t = t.AddDays(1))
                    {
                        double age = (t.AddDays(1) - created).TotalDays;  // i.e., what is the age of this bug at midnight?
                        if (!map.TryGetValue(t, out var entry))
                            map[t] = (age, 1);
                        else
                            map[t] = (entry.sumAges + age, entry.count + 1);
                    }
                }

                // finally, divide each sumAges value by the corresponding count
                foreach (var day in map.Keys)
                {
                    var (sumAges, count) = map[day];
                    map[day] = (sumAges/count, count);
                }
            }
            return map;
        }

        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: CSVtoABA.exe <file.csv> [optional end date]");
                return;
            }
            string csvPath = args[0];
            if (!File.Exists(csvPath))
            {
                Console.Error.WriteLine($"File not found: {csvPath}");
                return;
            }

            DateTime endDate;
            if (args.Length >= 2 && DateTime.TryParse(args[1], out var parsed))
                endDate = parsed;
            else
                endDate = DateTime.Now;

            if (Validate(csvPath) == false)
                return;

            var map = ComputeABA(csvPath, endDate);

            // output to screen for now
            Console.WriteLine("Date,Average Age of Open Bugs,Count of Open Bugs");
            foreach (var day in map.Keys.OrderBy(d => d))
            {
                var (aba, count) = map[day];
                Console.WriteLine($"{day:yyyy-MM-dd},{aba:F6},{count}");
            }
        }
    }
}
