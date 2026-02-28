using System;

namespace DeliveryRun.Managers.Subs
{
    internal sealed class HudActiveOrderStore
    {
        private readonly string[] _ids;
        private readonly string[] _texts;
        private int _count;

        internal HudActiveOrderStore(int capacity)
        {
            int size = capacity > 0 ? capacity : 1;
            _ids = new string[size];
            _texts = new string[size];
        }

        internal string[] Ids => _ids;
        internal string[] Texts => _texts;
        internal int Count => _count;
        internal string PrimaryOfferId => _count > 0 ? _ids[0] : null;

        internal void Upsert(string offerId, string text)
        {
            if (string.IsNullOrEmpty(offerId))
            {
                return;
            }

            int existingIndex = -1;
            for (int i = 0; i < _count; i++)
            {
                if (string.Equals(_ids[i], offerId, StringComparison.Ordinal))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                _texts[existingIndex] = text;
                return;
            }

            if (_count >= _ids.Length)
            {
                _count = _ids.Length - 1;
            }

            _ids[_count] = offerId;
            _texts[_count] = text;
            _count++;
        }

        internal void Remove(string offerId)
        {
            if (string.IsNullOrEmpty(offerId) || _count <= 0)
            {
                return;
            }

            int index = -1;
            for (int i = 0; i < _count; i++)
            {
                if (string.Equals(_ids[i], offerId, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return;
            }

            for (int i = index; i < _count - 1; i++)
            {
                _ids[i] = _ids[i + 1];
                _texts[i] = _texts[i + 1];
            }

            _count--;
            _ids[_count] = null;
            _texts[_count] = null;
        }

        internal void Clear()
        {
            for (int i = 0; i < _count; i++)
            {
                _ids[i] = null;
                _texts[i] = null;
            }

            _count = 0;
        }
    }
}
