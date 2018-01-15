﻿using BLL.Services;
using BLL.Services.Interface;
using BllEntities;
using DAL.Repositories;
using DAL.Repositories.Interface;
using ORM;
using ServerInterface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class BusinessService : IBusinessService
    {

        private static ServiceDB serviceDB;
        private static IUnitOfWork uow;

        private static Dictionary<string, IClientCallBack> Clients = new Dictionary<string, IClientCallBack>();
        private static object locker = new object();

        private const string SERVER_STATE = "Online";


        public static void Init()
        {
            serviceDB = new ServiceDB();
            uow = new UnitOfWork(serviceDB);

            uow.Events.GetAll();
        }

  
        public BllEvent CreateAndSendOutEvent(BllEvent Event)
        {
            var datetime = DateTime.Now;
            Event.Date = datetime;
            IEventService eventService = new EventService(uow);
            BllEvent res = eventService.Create(Event);
            new Thread(() =>
            {
                InvokeEventWithUsers(Event);
            }).Start();
            return res;

        }

        public List<BllEvent> GetEventsForUser(BllUser user)
        {
            IEventService eventService = new EventService(uow);
            return eventService.GetEventsForUser(user).ToList();
        }

        public IEnumerable<BllUser> GetUsersByGroup(BllGroup group)
        {
            IUserService userService = new UserService(uow);
            return userService.GetUsersByGroup(group.Id);
        }


        public BllEvent UpdateAcceptedUsersAndSendOutEvent(BllEvent Event, BllUser updater)
        {
            UserLibService userservice = new UserLibService(uow);
            Event.RecieverLib = userservice.Update(Event.RecieverLib);

            new Thread(() =>
            {
                UpdateEventWithUsers(Event, updater);
            }).Start();
            return Event;
        }

        public BllEvent UpdateStatusAndSendOutEvent(BllEvent Event, BllUser updater)
        {
            var datetime = DateTime.Now;
            Event.StatusLib.SelectedEntities.Last().Date = datetime;
            StatusLibService service = new StatusLibService(uow);
            Event.StatusLib = service.Update(Event.StatusLib);

            UserLibService userservice = new UserLibService(uow);
            Event.RecieverLib = userservice.Update(Event.RecieverLib);

            new Thread(() =>
            {
                UpdateEventWithUsers(Event, updater);
            }).Start();
            return Event;
        }



        private void UpdateEventWithUsers(BllEvent Event, BllUser updater)
        {
            foreach (var reciever in Event.RecieverLib.SelectedEntities)
            {
                try
                {
                    if (updater.Id != reciever.Entity.Id)
                    {
                        Clients[reciever.Entity.Login].UpdateEvent(Event);
                    }
                }
                catch (Exception ex)
                {
                    Clients.Remove(reciever.Entity.Login);
                }
            }
        }

        private void InvokeEventWithUsers(BllEvent Event)
        {
            foreach (var reciever in Event.RecieverLib.SelectedEntities)
            {
                try
                {
                    if (Event.Sender.Id != reciever.Entity.Id)
                    {
                        Clients[reciever.Entity.Login].GetEvent(Event);
                    }
                }
                catch (Exception ex)
                {
                    Clients.Remove(reciever.Entity.Login);
                }
            }
        }


        public void RegisterClient(string login)
        {
            if (login != null && login != "")
            {
                try
                {
                    IClientCallBack callback = OperationContext.Current.GetCallbackChannel<IClientCallBack>();
                    lock (locker)
                    {
                        //remove the old client
                        if (Clients.Keys.Contains(login))
                            Clients.Remove(login);
                        Clients.Add(login, callback);
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }

        public BllUser SignIn(string login, string password)
        {
            IUserService service = new UserService(uow);
            BllUser user =  service.Authorize(login, password);
            if (user != null)
            {
                RegisterClient(user.Login);
            }
            return user;
        }

        public static void PingClients()
        {
            lock (locker)
            {
                var inactiveClients = new List<string>();
                foreach (var client in Clients)
                {
                    try
                    {
                        client.Value.Ping() ;
                    }
                    catch (Exception ex)
                    {
                        inactiveClients.Add(client.Key);
                    }
                }

                if (inactiveClients.Count > 0)
                {
                    foreach (var client in inactiveClients)
                    {
                        Clients.Remove(client);
                    }
                }
            }
        }

        public string PingServer()
        {
            return SERVER_STATE;
        }

        #region StatusService
        public List<BllStatus> GetAllStatuses()
        {
            IStatusService statusService = new StatusService(uow);
            return statusService.GetAll().ToList();
        }

        public BllStatus CreateStatus(BllStatus entity)
        {
            IStatusService service = new StatusService(uow);
            return service.Create(entity);
        }

        public void DeleteStatus(int id)
        {
            IStatusService service = new StatusService(uow);
            service.Delete(id);
        }

        public BllStatus UpdateStatus(BllStatus entity)
        {
            IStatusService service = new StatusService(uow);
            return service.Update(entity);
        }
        #endregion

        #region AttributeService
        public List<BllAttribute> GetAllAttributes()
        {
            IAttributeService AttributeService = new AttributeService(uow);
            return AttributeService.GetAll().ToList();
        }

        public BllAttribute CreateAttribute(BllAttribute entity)
        {
            IAttributeService service = new AttributeService(uow);
            return service.Create(entity);
        }

        public void DeleteAttribute(int id)
        {
            IAttributeService service = new AttributeService(uow);
            service.Delete(id);
        }

        public BllAttribute UpdateAttribute(BllAttribute entity)
        {
            IAttributeService service = new AttributeService(uow);
            return service.Update(entity);
        }
        #endregion

        #region GroupService
        public List<BllGroup> GetAllGroups()
        {
            IGroupService GroupService = new GroupService(uow);
            return GroupService.GetAll().ToList();
        }

        public BllGroup CreateGroup(BllGroup entity)
        {
            IGroupService service = new GroupService(uow);
            return service.Create(entity);
        }

        public void DeleteGroup(int id)
        {
            IGroupService service = new GroupService(uow);
            service.Delete(id);
        }

        public BllGroup UpdateGroup(BllGroup entity)
        {
            IGroupService service = new GroupService(uow);
            return service.Update(entity);
        }
        #endregion

        #region UserService
        public List<BllUser> GetAllUsers()
        {
            IUserService UserService = new UserService(uow);
            return UserService.GetAll().ToList();
        }

        public BllUser CreateUser(BllUser entity)
        {
            IUserService service = new UserService(uow);
            return service.Create(entity);
        }

        public void DeleteUser(int id)
        {
            IUserService service = new UserService(uow);
            service.Delete(id);
        }

        public BllUser UpdateUser(BllUser entity)
        {
            IUserService service = new UserService(uow);
            return service.Update(entity);
        }
        #endregion

        #region EventTypeService
        public List<BllEventType> GetAllEventTypes()
        {
            IEventTypeService EventTypeService = new EventTypeService(uow);
            return EventTypeService.GetAll().ToList();
        }

        public BllEventType CreateEventType(BllEventType entity)
        {
            IEventTypeService service = new EventTypeService(uow);
            return service.Create(entity);
        }

        public void DeleteEventType(int id)
        {
            IEventTypeService service = new EventTypeService(uow);
            service.Delete(id);
        }

        public BllEventType UpdateEventType(BllEventType entity)
        {
            IEventTypeService service = new EventTypeService(uow);
            return service.Update(entity);
        }
        #endregion

    }
}
