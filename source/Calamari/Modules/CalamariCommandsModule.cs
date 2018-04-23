﻿using Autofac;
using Calamari.Commands.Support;

namespace Calamari.Modules
{
    /// <summary>
    /// Autofac module to register the calamari commands
    /// </summary>
    class CalamariCommandsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(ThisAssembly).AssignableTo<ICommand>().As<ICommand>().SingleInstance();
        }
    }
}
