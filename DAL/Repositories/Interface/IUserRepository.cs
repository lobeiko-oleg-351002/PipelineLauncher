﻿using DAL.Entities;
using ORM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAL.Repositories.Interface
{
    public interface IUserRepository : IRepository<DalUser, User>
    {
        DalUser GetUserByLogin(string login);
        DalUser Authorize(string login, string password);
        IEnumerable<DalUser> GetUsersByGroup(int group_id);
    }
}
