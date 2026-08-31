namespace MineVent.Domain
{
    public class Airway
    {
        public int Id;
        public int StartNodeId;
        public int EndNodeId;

        public float R;             // resistance
        public float Q;             // signed airflow; positive = StartNodeId -> EndNodeId
        public float FanPressure;   // defaults to 0.0 (no fan)

        public Airway(int id, int startNodeId, int endNodeId, float resistance, float initialQ = 0f, float fanPressure = 0f)
        {
            Id = id;
            StartNodeId = startNodeId;
            EndNodeId = endNodeId;
            R = resistance;
            Q = initialQ;
            FanPressure = fanPressure;
        }

        // Atkinson's equation: P = R*Q*|Q|, minus any fan boost.
        // |Q| preserves the correct sign of pressure drop for reversed flow.
        public float Pressure => R * Q * System.Math.Abs(Q) - FanPressure;
    }
}