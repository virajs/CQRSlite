﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CQRSlite.Contracts.Bus;
using CQRSlite.Contracts.Bus.Handlers;
using CQRSlite.Contracts.Infrastructure.DI;

namespace CQRSlite.Bus
{
    public class BusRegistrar
    {
        private readonly IServiceLocator _serviceLocator;

        public BusRegistrar(IServiceLocator serviceLocator)
        {
            _serviceLocator = serviceLocator;
        }

        public void Register(params Type[] typesFromAssemblyContainingMessages)
        {
            var bus = _serviceLocator.GetService<IHandlerRegistrar>();
            
            foreach (var typesFromAssemblyContainingMessage in typesFromAssemblyContainingMessages)
            {
                var executorsAssembly = typesFromAssemblyContainingMessage.Assembly;
                var executorTypes = executorsAssembly
                    .GetTypes()
                    .Select(t => new { Type = t, Interfaces = ResolveMessageHandlerInterface(t) })
                    .Where(e => e.Interfaces != null && e.Interfaces.Any());

                foreach (var executorType in executorTypes)
                    foreach (var @interface in executorType.Interfaces)
                        InvokeHandler(@interface, bus, executorType.Type);
            }
        }

        private void InvokeHandler(Type @interface, IHandlerRegistrar bus, Type executorType) {
            var commandType = @interface.GetGenericArguments()[0];

            var registerExecutorMethod = bus
                .GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(mi => mi.Name == "RegisterHandler")
                .Where(mi => mi.IsGenericMethod)
                .Where(mi => mi.GetGenericArguments().Count() == 1)
                .Single(mi => mi.GetParameters().Count() == 1)
                .MakeGenericMethod(commandType);

            var del = new Action<dynamic>(x =>
                                              {
                                                  dynamic handler = _serviceLocator.GetService(executorType);
                                                  handler.Handle(x);
                                              });
            
            registerExecutorMethod.Invoke(bus, new object[] { del });
        }

        private static IEnumerable<Type> ResolveMessageHandlerInterface(Type type)
        {
            return type
                .GetInterfaces()
                .Where(i => i.IsGenericType && ((i.GetGenericTypeDefinition() == typeof(HandlesCommand<>))
												 || i.GetGenericTypeDefinition() == typeof(HandlesEvent<>)));
        }

    }
}
