using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Services.Shipping.Tracking;

namespace NopBrasil.Plugin.Shipping.Correios
{
    public class CorreiosShipmentTracker : IShipmentTracker
    {
        private readonly CorreiosSettings _correiosSettings;

        public CorreiosShipmentTracker(CorreiosSettings correiosSettings)
        {
            this._correiosSettings = correiosSettings;
        }

        public virtual bool IsMatch(string trackingNumber)
        {
            throw new NotImplementedException("");
        }

        public virtual string GetUrl(string trackingNumber)
        {
            return $"https://melhorrastreio.com.br/rastreio/{trackingNumber}";
        }

        public virtual IList<ShipmentStatusEvent> GetShipmentEvents(string trackingNumber)
        {
            return new List<ShipmentStatusEvent>();
        }

        public Task<bool> IsMatchAsync(string trackingNumber)
        {
            throw new NotImplementedException();
        }

        public async Task<string> GetUrlAsync(string trackingNumber)
        {
            return $"https://melhorrastreio.com.br/rastreio/{trackingNumber}";
        }

        public Task<IList<ShipmentStatusEvent>> GetShipmentEventsAsync(string trackingNumber)
        {
            throw new NotImplementedException();
        }
    }
}