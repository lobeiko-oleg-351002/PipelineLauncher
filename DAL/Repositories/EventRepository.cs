﻿using DAL.Entities;
using DAL.Mapping;
using DAL.Repositories.Interface;
using Globals;
using ORM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAL.Repositories
{
    public class EventRepository : Repository<DalEvent, Event, EventMapper>, IEventRepository
    {
        private readonly ServiceDB context;
        public EventRepository(ServiceDB context) : base(context)
        {
            this.context = context;
        }

        public IEnumerable<DalEvent> GetEventsForUser(int user_id)
        {
            EventMapper mapper = new EventMapper();
            //var user = context.Set<User>().FirstOrDefault(entity => entity.id == user_id);
            //foreach (var element in user.SelectedUser)
            //{
            //    var ev = element.UserLib.Event;
            //    if (ev.Count != 0 && ev.Status != delted or closed)
            //    {
            //        retElemets.Add(mapper.MapToDal(ev.First()));
            //    }
            //}
            var events = context.Set<Event>().Where(entity =>
                                (entity.StatusLib.SelectedStatus.OrderByDescending(o => o.id).FirstOrDefault().Status.name != Globals.Globals.STATUS_CLOSED)
                                && (entity.StatusLib.SelectedStatus.OrderByDescending(o => o.id).FirstOrDefault().Status.name != Globals.Globals.STATUS_DELETED)
                                && entity.UserLib.SelectedUser.Any(e => e.User.id == user_id));            
            var retElemets = new List<DalEvent>();
            foreach(var item in events)
            {
                retElemets.Add(mapper.MapToDal(item));
            }

            return retElemets;       
        }
    }
}
