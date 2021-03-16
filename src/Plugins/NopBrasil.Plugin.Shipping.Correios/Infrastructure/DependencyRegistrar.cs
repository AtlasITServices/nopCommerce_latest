using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using NopBrasil.Plugin.Shipping.Correios.Service;

namespace NopBrasil.Plugin.Shipping.Correios.Infrastructure
{
    public class DependencyRegistrar : IDependencyRegistrar
    {
        //public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig nopConfig)
        //{
        //    builder.RegisterType<CorreiosService>().As<ICorreiosService>().InstancePerDependency();
        //    builder.RegisterType<CorreiosComputationMethod>().InstancePerLifetimeScope();
        //}

        public void Register(IServiceCollection services, ITypeFinder typeFinder, AppSettings appSettings)
        {
            // TODO: verificar forma correta de implementar, a forma abaixo estoura erro e só foi adicionada pra compilar o código :-P
            services.AddScoped<CorreiosService>();
            services.AddScoped<CorreiosComputationMethod>();
        }

        public int Order => 2;
    }
}
