# Notes

I focused on getting the main requirements working first, such as connecting to SQL Server, displaying the device configurations in a readable way, allowing a user to search for a specific device by ID, and handling invalid inputs or database errors.

One of the challenges for me was that I had not really worked with C#, .NET, or SQL Server before. Most of my previous experience was with C++, PostgreSQL, MySQL, and Supabase. I had to do some research and learn how C# and .NET work, as well as how to connect to and query SQL Server, before I could properly put the application together. Once I understood the basics, I was able to apply what I already knew about programming and databases to the task.

I also paid attention to the sample data because some of the records contain missing or unusual values. Instead of silently filling in missing values, I added validation so these issues can be identified when the application runs.

If I had more time, I would add more automated testing for things like invalid device IDs, missing configuration values, devices that do not exist, and database connection failures. I would also improve the structure of the code by separating the database, validation, and display logic.

I would also explore using AI to help identify suspicious or uncommon device configuration entries, especially if the amount of device data became much larger. AI could be trained to learn what normal configuration values look like and flag entries that are unusual for further checking.

Some of the validation rules I used are based on reasonable assumptions from the sample data, so I would confirm these rules with the relevant team before using them in a real system. I would also improve the security setup by using a database account with only the permissions required by the application instead of using the `sa` account.

I prioritised getting the application working properly, understanding the technologies I was less familiar with. With more time, I would focus on improving the testing, code structure, and detection of suspicious or unusual configurations.