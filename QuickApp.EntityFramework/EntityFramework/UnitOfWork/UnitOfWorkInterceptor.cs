﻿using Castle.DynamicProxy;
using log4net;
using System;
using System.Data.Entity.Validation;
using System.Reflection;
using QuickApp.EntityFramework.EntityFramework.UnitOfWork;

namespace QuickApp.EntityFramework.EntityFramework.UnitOfWork
{
    public class UnitOfWorkInterceptor : IInterceptor
    {
        protected static readonly ILog _logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.Name);

        public void Intercept(IInvocation invocation)
        {
            if (invocation.MethodInvocationTarget.DeclaringType != null)
            {
                _logger.Debug(invocation.MethodInvocationTarget.DeclaringType.Name + " -> " + invocation.MethodInvocationTarget.Name);
            }
            //If there is a running transaction, just run the method
            if (EfUnitOfWorkQuickAppBase.Instance.Context != null)
            {
                invocation.Proceed();
                return;
            }
            //The context should be created for per thread/request.
            //_logger.Debug("EfUnitOfWorkSampleContextBase.Instance Hash : " + EfUnitOfWorkQuickAppBase.Instance.GetHashCode());
            var useTransaction = true;
            var uowAttributes = invocation.MethodInvocationTarget.GetCustomAttributes(typeof(Core.Attributes.UnitOfWork), true);
            if (uowAttributes.Length > 0)//Disable the transaction by using DisableTransaction property of the attribute
                if (((Core.Attributes.UnitOfWork)uowAttributes[0]).DisableTransaction)
                    useTransaction = false;
            try
            {
                EfUnitOfWorkQuickAppBase.Instance.UseTransaction = useTransaction;
                EfUnitOfWorkQuickAppBase.Instance.BeginTransaction();
                try
                {
                    invocation.Proceed();
                    EfUnitOfWorkQuickAppBase.Instance.CommitTransaction();
                }
                catch (Exception e)
                {
                    _logger.Error(e.Message, e);
                    if (e is DbEntityValidationException ex)
                    {
                        foreach (var eve in ex.EntityValidationErrors)
                        {
                            var error = string.Format("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                                eve.Entry.Entity.GetType().Name, eve.Entry.State);
                            _logger.Error(error);
                            foreach (var ve in eve.ValidationErrors)
                            {
                                var innerError = string.Format("- Property: \"{0}\", Error: \"{1}\"",
                                    ve.PropertyName, ve.ErrorMessage);
                                _logger.Error(innerError);
                            }
                        }
                    }
                    throw;
                }
            }
            finally
            {
                EfUnitOfWorkQuickAppBase.Instance.Dispose();
            }
        }
    }
}
