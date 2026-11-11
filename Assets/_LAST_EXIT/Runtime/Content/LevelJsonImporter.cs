using System;
using System.Collections.Generic;
using System.Globalization;
using LastExit.Domain;
using UnityEngine;

namespace LastExit.Content
{
    public static class LevelJsonImporter
    {
        public static LevelDefinition Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Level JSON is required.", nameof(json));
            }

            LevelData data = new LevelData();
            JsonUtility.FromJsonOverwrite(json, data);
            ValidateHeader(data);
            Dictionary<string, int> corridors = new Dictionary<string, int>
            {
                { "A", data.grid.corridors.A },
                { "B", data.grid.corridors.B },
                { "C", data.grid.corridors.C },
                { "D", data.grid.corridors.D }
            };

            List<QueueDefinition> queues = new List<QueueDefinition>();

            foreach (QueueData queue in data.queues)
            {
                if (queue == null || queue.passengers == null || queue.passengers.Length == 0)
                {
                    throw new ArgumentException("Each configured queue requires passengers.", nameof(json));
                }

                List<PassengerDefinition> passengers = new List<PassengerDefinition>();

                foreach (PassengerData passenger in queue.passengers)
                {
                    if (passenger == null)
                    {
                        throw new ArgumentException("A passenger definition cannot be null.", nameof(json));
                    }

                    passengers.Add(new PassengerDefinition(passenger.id, passenger.route, passenger.kind));
                }

                queues.Add(new QueueDefinition(queue.id, passengers));
            }

            List<BusDefinition> buses = new List<BusDefinition>();

            foreach (BusData bus in data.buses)
            {
                if (bus == null)
                {
                    throw new ArgumentException("A bus definition cannot be null.", nameof(json));
                }

                buses.Add(new BusDefinition(bus.id, bus.route, bus.@class, bus.footprint_cells,
                    bus.capacity, bus.initial_left_x));
            }

            List<RowDefinition> rows = new List<RowDefinition>();

            foreach (RowData row in data.rows)
            {
                if (row == null)
                {
                    throw new ArgumentException("Each row requires an initial bus.", nameof(json));
                }

                rows.Add(new RowDefinition(row.id, row.initial_bus_id));
            }

            return new LevelDefinition(data.level_id, data.grid.width, corridors, queues, buses, rows,
                data.cctv_charges, data.initial_stability, data.target_cctv_uses, data.next_bus_queue);
        }

        private static void ValidateHeader(LevelData data)
        {
            if (data.schema_version != "0.4" || data.status != "prototype_candidate")
            {
                throw new ArgumentException("Expected prototype_candidate level schema 0.4.");
            }

            if (data.level_index < 1 || data.level_index > 15
                || data.level_id != "LEVEL-" + data.level_index.ToString("00", CultureInfo.InvariantCulture))
            {
                throw new ArgumentException("Level ID and index must identify the same level in 01 to 15.");
            }

            if (data.grid == null || data.grid.corridors == null || data.grid.width < 10 || data.grid.width > 30
                || data.queues == null || data.buses == null || data.rows == null || data.next_bus_queue == null)
            {
                throw new ArgumentException("Level grid, queues, buses and rows are required.");
            }

            if (data.cctv_charges < 0 || data.target_cctv_uses < 0 || data.target_cctv_uses > data.cctv_charges
                || data.initial_stability < 1 || data.initial_stability > 3)
            {
                throw new ArgumentException("Invalid CCTV or stability configuration.");
            }
        }

        // DTO fields intentionally mirror the external snake_case contract. QA reference_solution is not loaded.
#pragma warning disable CS0649
        [Serializable]
        private class LevelData
        {
            public string schema_version;
            public string status;
            public string level_id;
            public int level_index;
            public GridData grid;
            public QueueData[] queues;
            public BusData[] buses;
            public RowData[] rows;
            public string[] next_bus_queue;
            public int cctv_charges = -1;
            public int initial_stability;
            public int target_cctv_uses = -1;
        }

        [Serializable]
        private class GridData
        {
            public int width;
            public CorridorData corridors;
        }

        [Serializable]
        private class CorridorData
        {
            public int A = -1;
            public int B = -1;
            public int C = -1;
            public int D = -1;
        }

        [Serializable]
        private class QueueData
        {
            public string id;
            public PassengerData[] passengers;
        }

        [Serializable]
        private class PassengerData
        {
            public string id;
            public string route;
            public string kind;
        }

        [Serializable]
        private class BusData
        {
            public string id;
            public string route;
            public string @class;
            public int footprint_cells;
            public int capacity;
            public int initial_left_x = -1;
        }

        [Serializable]
        private class RowData
        {
            public string id;
            public string initial_bus_id;
        }
#pragma warning restore CS0649
    }
}
