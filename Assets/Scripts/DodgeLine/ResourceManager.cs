using UnityEngine;

namespace DodgeLine
{
    public sealed class ResourceManager : MonoBehaviour
    {
        public int Energy { get; private set; }
        private float incomeRemainder;
        public void ResetBalance(int amount) { Energy = amount; incomeRemainder = 0; }
        public bool TrySpend(int amount)
        {
            if (amount < 0 || Energy < amount) return false;
            Energy -= amount;
            return true;
        }
        public void Add(int amount) { Energy += Mathf.Max(0, amount); }
        public void TickIncome(float seconds, float perSecond)
        {
            incomeRemainder += seconds * perSecond;
            int whole = Mathf.FloorToInt(incomeRemainder + .00001f);
            Energy += whole;
            incomeRemainder -= whole;
        }
    }
}
