# Battleships

## About
This is backend for "Battleships" game. It is responsible for managing lobbies that players can create and join. There can be only two players in the same lobby. You can read the logs displayed in application console while testing it.

## Getting started
Everything you have to do is to download this project and run the application. 

If you have any problem with running this application it can be due to CSRF. If that's the case, you have to make sure that [`BattleshipsFrontend`](https://github.com/KrzychuK121/BattleshipsFrontend) has the same address as your currently working backend (you can localize this declaration in `src/HubProvider/HubProvider.jsx`).

## Full project guide
This game has frontend and backed separated into two different GitHub repositories. To run and test the application you should follow these steps:
1. Download [backend](https://github.com/KrzychuK121/BattleshipsBackend)
2. Run backend application and check running address
3. Download [frontend](https://github.com/KrzychuK121/BattleshipsFrontend)
4. Run frontend application and make sure that in `src/HubProvider/HubProvider.jsx` you have defined the same address as address from 2. step
5. Enjoy the game
