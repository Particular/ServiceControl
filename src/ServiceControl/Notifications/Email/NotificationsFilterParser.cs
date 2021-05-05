namespace ServiceControl.Notifications.Email
{
    using System.Collections.Generic;

    class NotificationsFilterParser
    {
        public static string[] Parse(string filter)
        {
            var ids = new List<string>();

            var delimiter = '#';
            var currentId = string.Empty;

            for (var i = 0; i < filter.Length; i++)
            {
                if (filter[i] != delimiter)
                {
                    currentId += filter[i];
                }
                else
                {
                    //We arrived at escaped delimiter
                    if (i + 1 < filter.Length && filter[i + 1] == delimiter)
                    {
                        currentId += filter[i++];
                    }
                    else
                    {
                        if (currentId != string.Empty)
                        {
                            ids.Add(currentId);
                        }

                        currentId = string.Empty;
                    }
                }
            }

            if (currentId != string.Empty)
            {
                ids.Add(currentId);
            }

            return ids.ToArray();
        }
    }
}