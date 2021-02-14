﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using SeleniumLib;
using System.Windows.Forms;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices.WindowsRuntime;

namespace InstagramSeleniumBot
{
    public class Bot
    {
        private protected Writer Cons;
        private protected DBmanager db;
        private protected ChromeBrowser Chrome { get; set; }
        private protected static Random rand;
        public bool stop;
        private protected CancellationToken token;

        public Bot(Writer console, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            this.token = token;
            Cons = console;

            Cons.WriteLine($"Загрузка бота.");
            db = new DBmanager(console);
            rand = new Random();
            Chrome = new ChromeBrowser("InstagramBotProfile");
            Chrome.SetWindowSize(500, 1000);
            Cons.WriteLine($"Бот загружен.");
        }


        public void Autorize(string login, string password)
        {
            if (token.IsCancellationRequested)
                return;

            db.Name = login;
            int delay = rand.Next(100);
            Thread.Sleep(delay);
            CheckConnectInternet();
            Thread.Sleep(delay);
            Chrome.OpenUrl(@"https://www.instagram.com/?hl=ru");
         
            if (Chrome.FindWebElement(By.XPath("//a[@href ='/direct/inbox/']"))!=null)
            {
                Cons.WriteLine($"Пользователь авторизован.");
                return;
            }

            Cons.WriteLine($"Авторизация пользователя");
            Chrome.SendKeysXPath(@"//*[@id='loginForm']/div/div[1]/div/label/input", login);
            Chrome.SendKeysXPath(@"//*[@id='loginForm']/div/div[2]/div/label/input", password);
            Thread.Sleep(300);
            Chrome.ClickButtonXPath(@"//*[@id='loginForm']/div/div[3]/button");

            CheckAutorize();
        }

        private void CheckAutorize()
        {
            if (token.IsCancellationRequested)
                return;

            Cons.WriteLine($"Проверка авторизации.");
            if (Chrome.FindWebElement(By.XPath("//a[@href ='/direct/inbox/']")) == null)
            {
                Cons.WriteLine($"Ошибка авторизации.");
                Close();
            }
            Cons.WriteLine($"Авторизовано.");
        }

        public void Close()
        {
            if(Chrome != null)
                Chrome.Quit();
        }

        internal int ParsToInt(string text)
        {
            if (!Int32.TryParse(text, out _))
            {
                text = text.Replace(" ", string.Empty);
                text = text.Replace("тыс.", "000");
                if (text.IndexOf(',') > 0)
                {
                    text = text.Replace(",", string.Empty);
                    text = text.Substring(0, text.Length - 1);
                }
            }

            if (!Int32.TryParse(text, out int num))
                Cons.WriteLine($"Ошибка парсинга строки в число - {text}");

            return num;
        }

        public bool CheckConnectInternet()
        {
            if (token.IsCancellationRequested)
                return true;

            var ping = new Ping();
            PingReply reply = ping.Send("instagram.com");

            if (Convert.ToString(reply.Status) != "Success")
            {
                reply = ping.Send("instagram.com");
                Thread.Sleep(2000);
                if (Convert.ToString(reply.Status) != "Success")
                {
                    Cons.WriteLine($"Ошибка соеденения с сайтом");
                    Close();
                    return false;
                }
            }

            return true;
        }

        public void CollectingAccounts(int limit)
        {
            string pathScroll;
            if (token.IsCancellationRequested)
                return;
           
            db.GetStatisticDB();

            for (int j = 0; j < 30; j++)
            {
                if (!OpenRandSubsPage())
                    return;

                Thread.Sleep(2000 + rand.Next(100));
                Chrome.MoveToElement(Chrome.FindWebElement(By.XPath($"/html/body/div[5]/div/div/div[1]")));

                for (int i = 1; i <= limit + 1; i++)
                {
                    if (token.IsCancellationRequested)
                        return;

                    Thread.Sleep(50 + rand.Next(100));

                    pathScroll = $"//ul/div/li[{i}]";

                    if (!Chrome.FindWebElement(By.XPath(pathScroll)).Displayed)
                        break;

                    Cons.WriteLine($"Scroll - {i}.");

                    if (!Chrome.Scroll(pathScroll))
                    {
                        Cons.WriteError($"Не удается проскролить.");
                        return;
                    }

                    db.AddNewUrl(ParseUrlAccount(i));

                    if (i == limit)
                    {
                        Cons.WriteLine($"Сбор завершен.");
                        return;
                    }
                }
               
            }
            
        }

        internal bool OpenRandSubsPage()
        {
          
            for (int j = 0; j < 5; j++)
            {
                if (!db.GetRandomUrl(out string rand_url))
                    return false;
                Cons.WriteLine($"OpenRandSubsPage {rand_url}.");

                Chrome.OpenUrl(rand_url);
              
                if (Chrome.ClickButtonXPath("//a[contains(.,'подписчиков')]"))
                    return true;

            }
            return false;
        }

        internal string ParseUrlAccount(int i)
        {
            if (token.IsCancellationRequested)
                return null;
           
            string path = $@"//ul/div/li[{i}]//a";
            IWebElement element = Chrome.FindWebElement(By.XPath(path));
            if (element == null)
            {
                Cons.WriteError("Ссылка для подписок не найдена.");
                return null;
            }
         
            string url = element.GetAttribute("href");
            if (url == null || url == "")
            {
                Cons.WriteError("Ссылка на подписчика пуста.");
                return null;
            }
           
            Cons.WriteLine($"Add {url.Trim()}");
            return url;
        }

        public void ProcessingAccounts(int limit)
        {
            if (token.IsCancellationRequested)
                return;
            db.GetStatisticDB();
            List<string> url = db.GetUrlAwaitingList(limit);      //Получить список аккаунтов
            for (int i = 0; i < url.Count; i++)
            {
                if (token.IsCancellationRequested)
                    return;

                Cons.WriteLine($"{i}) {url[i].Trim()}");
                Chrome.OpenUrl(url[i]);

                if (!Parsing(url[i]))
                    break;
            }
        }

        internal bool Parsing(string url)
        {
            if (token.IsCancellationRequested)
                return false;

            //проверка  загрузки страницы
            if (Chrome.FindWebElement(By.XPath(@"/html/body/div")) == null)
            {
                Cons.WriteError($"Не удалось загрузить страницу.");
                return false;
            }

            //проверка существования страницы
            if (Chrome.IsElementPage(By.LinkText("Назад в Instagram.")))
            {
                Cons.WriteLine($"Страницы не существует.");
                db.MakeUrlNotInterest(url);
                return true;
            }

            //Проверка страницы на открытость
            string xPath = @"//*[@id='react-root']/section/main/div/ul/li[2]/span/span";
            if (Chrome.IsElementPage(By.XPath(xPath)))
            {
                Cons.WriteLine($"Страница закрыта.");
                db.MakeUrlNotInterest(url);
                return true;
            }

            if (Chrome.FindWebElement(By.XPath(@"//button[contains(.,'Подписаться')]")) == null)
            {
                Cons.WriteLine($"Кнопка подписаться не найдена.");
                db.MakeUrlNotInterest(url);
                return true;
            }

            int num_subs = ParseSpanTextToInt("подписчиков");       //Парссинг количества подписчиков
            int num_subc = ParseSpanTextToInt("подписок");          //Парссинг количества подписок

            //забираем для списка подписок
            if (num_subs > 400)
            {
                Cons.WriteLine($"Забираем для списка подписок.");
                db.AddUrlInGetDonorSubsList(url);
                return true;
            }

            //Забираем для подписки
            if (num_subs > 40 && num_subs < 200 && num_subs < 2000 && num_subc / num_subs >= 2)
            {
                Cons.WriteLine($"Забираем для подписки.");
                db.AddUrlInMyFutereSubs(url);
                return true;
            }

            db.MakeUrlNotInterest(url);
            return true;
        }

        private int ParseSpanTextToInt(string s)
        {
            int num = 0;
            IWebElement element = Chrome.FindWebElement(By.XPath($@"//a[contains(.,'{s}')]/span"));
            if (element == null)
                element = Chrome.FindWebElement(By.XPath(@"//span[contains(.,'подписчиков')]/span"));
            if (element != null)
                num = ParsToInt(element.Text);
            return num;
        }

        public void SubscribeAccounts(int limit)
        {
            if (token.IsCancellationRequested)
                return;
            db.GetStatisticDB();
            for (int i = 0; i < limit; i++)
            {
                if (token.IsCancellationRequested)
                    return;

                if (!db.GetUrlForSubscribe(out string url))
                    return;

                Cons.WriteLine($"{i}).Подписка на {url.Trim()}");
                Chrome.OpenUrl(url);

                if (!Subscribe(url))
                    return;
            }
        }

        private bool Subscribe(string url)
        {
            if (token.IsCancellationRequested)
                return false;

#if DEBUG
            Thread.Sleep(TimeSpan.FromSeconds(1));
#else
             Thread.Sleep(TimeSpan.FromSeconds(15 + rand.Next(15)));
#endif

            IWebElement element = Chrome.FindWebElement(By.XPath(@"//button[contains(.,'Подписаться')]"));
            if (element == null)
            {
                Cons.WriteLine($"Не удалось подписаться.");
                db.MakeUrlNotInterest(url);
                return true;
            }
            //Проверка на друзей 

            element.Click();

#if DEBUG
            Thread.Sleep(TimeSpan.FromSeconds(1));
#else
             Thread.Sleep(TimeSpan.FromSeconds(6));
#endif


            element = Chrome.FindWebElement(By.XPath(@"//button[contains(.,'Отправить сообщение')]"));
            if (element != null)
            {
                db.AddUrlToFriend(url);
                return true;
            }

            element = Chrome.FindWebElement(By.XPath(@"//button[contains(.,'Запрос отправлен')]"));
            if (element != null)
            {
                db.AddUrlToFriend(url);
                return true;
            }


            Cons.WriteLine($"Не удалось подписаться.");
            element = Chrome.FindWebElement(By.XPath(@"//button[contains(.,'Сообщить о проблеме')]"));
            if (element != null)
            {
                Cons.WriteLine($"Превышен лимит подписок.");
                return false;
            }
            db.AddUrlToFriend(url);
            return true;
        }

        public void UnSubscribeAccounts(int limit)
        {
            if (token.IsCancellationRequested)
                return;
            db.GetStatisticDB();

            for (int i = 0; i < limit; i++)
            {
                if (token.IsCancellationRequested)
                    return;

                if (!db.GetUrlForUnSubscribe(out string url))
                    return;

                UnSubscribe(url, i);
            }
        }

        public bool UnSubscribe(string url, int i)
        {
            if (token.IsCancellationRequested)
                return true;

            db.MakeUrlNotInterest(url);

            IWebElement element;
            Chrome.OpenUrl(url);

            Thread.Sleep(TimeSpan.FromSeconds(2));
            if (Chrome.IsElementPage(By.LinkText("Назад в Instagram.")))
            {
                Cons.WriteLine($"Страницы не существует.");
                return true;
            }

            string xpath = @"//*[@class='FLeXg bqE32']//button";
            Chrome.FindWebElement(By.XPath(xpath));

            Cons.WriteLine($"Отписка {i} - {url.Trim()}");

            Thread.Sleep(TimeSpan.FromSeconds(5 + rand.Next(10)));
            Chrome.ClickButtonXPath(xpath);
            Thread.Sleep(TimeSpan.FromSeconds(2));
            Chrome.ClickButtonXPath(@"//button[contains(.,'Отменить подписку')]");
            Thread.Sleep(TimeSpan.FromSeconds(3));

            element = Chrome.FindWebElement(By.XPath(@"//button[contains(.,'Подписаться')]"));
            if (element == null)
            {
                Cons.WriteLine($"Не удалось отписаться.");
                return false;
            }
            Cons.WriteLine($"Отписаны.");
            return true;
        }

    }



}
