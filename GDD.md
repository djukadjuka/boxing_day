# Game Overview

- **Title**: Boxing Day
- **Genre**: Simulation/Puzzle
- **Platform**: PC, Console
- **Target group**: Single player gamers, 16+ (FUTURE: Coop)
- **Premise**: Move and stack as many boxes in designated areas before the end of your shift to earn money. Use money for resources to survive, and improve your gear in order to earn more money.

# Story / Setting

- **Setting**: Fictional State of ``Bellmont``. Small town ``Harbor Glen``, close and west of the capitol of ``Bellmont`` - ``Highmark City``. The year is 1990
- **Premise**: 
	You are a regular guy who lost his job at a home-improvement store that shut down - ``(Mercer Homeworks)``. You found a new job stacking and organizing boxes and crates in another company's warehouse (``Cartwright Trading Co.`` - A domestic retail chain offering various groceries, fresh food, and household items)
- **Player Role**: Warehouse worker
- **Main Goal**: **`Getting a promotion ends the main story of the game.`** Do your job until your shift ends to earn money for groceries and to pay the bills for survival. Any extra money you don't spend on basic life necessities can be spent on upgrading your skills and buying new tools or upgrading existing tools. Your player wants a better position, which he can earn by working hard.

# Gameplay
### Core Mechanics: 

- Movement (basic WASD, jumping, sprinting) 
- Picking up/placing/rotating objects
- World interaction through characters and machines

### Game Loop: 

- Player enters locker room to choose upgrades
- Player can check the warehouse layout (where stacks can be made and of what material/goods, and where new stock is imported)
- Player clocks in for shift - first load of goods is immediately imported and shift timer starts
- Player picks up stock from truck pallete at import station (player chooses which boxes to pick up through in game menu/inventory of truck)
- Player uses a means to transport box to stacking position (means - on foot, using forklift, pulley, any machine that can help; position - depending on box, can be anywhere on rack, fridge, special height depending on box etc.)
- During shift, player can interact with world objects such as machines, toolbox, toolbelt, cafeteria machines (coffee maker, fridge..) to get bonuses and buffs/debuffs
- When time runs out, players shift ends and player receives progress report
- Doors to the warehouse floor are locked and the player is placed in the main hall of the warehouse
- The player can move to the pantry, cafeteria, or the security room of the warehouse or exit and end the day
- Player enters apartment (screen) and can distribute money on expenses and upgrades
- Player ends the day with a report of his quality of life and next day buffs and debuffs

### Controls: 

- Movement: WASD basic movement, shift for faster running, space for jumping
- Action: E - for interacting with any and all objects in the game world
- Looking: Using the mouse
- Object rotation: Holding middle mouse while moving the mouse and while holding an object
- Advanced world interaction: Machines, tools and consoles in the world have their controls printed out on the objects themselves, or in a 'polite' position that the player can see and interpret
- F1: Help; Opens the help page for the object the player is pointing at with their mouse. Contains pages of text that the user can navigate and interpret. This action pauses the game
- Escape: Pauses the game and opens the in game menu

### Difficulty:

- Casual endless mode - Low difficulty with random level-independant layouts. Active after all possible game levels have been unlocked
- Story mode - Low difficulty with tutorial level. Scales difficulty as days pass on
- Hardcore mode - Possible future upgrade - No HUD, stamina and other buffs are drastically lower, time is shorter and work is harder

# Characters
- Player Character: The worker (players choose name at the start of the game)
- Enemies: None
- NPCs:
	- `Larry`, `Terry`: Truck divers that park the truck into loading bays
	- `Stan Cobb` (Teacher): Warehouse foreman - shows the player the ropes
	- `Kate` (Support): Part-time cleaner and supply manager - Stocks warehouse supplies and cleans the warehouse - Can help player in various ways related to her role
	- `Jack Stone` (Support): Night shift guard - Watches over the warehouse during the night - Can help player in various ways related to his role  

# Progression & Systems

## Leveling

The player earns money the more boxes they stack correctly during their shift. Money is used for various mechanics in the game that have an effect on the player. 

The levels are expressed as 'days'. Each day, the shift is more difficult:
- Warehouse stack zones are different
- Warehouse gets larger and offers more stacking space with more randomness
- More facilites are opened in the warehouse (freezer, garden, floor storage, pallet zone)
- Loading zones may open/close/breakdown influencing where the player must go for new boxes
- Items that are delivered to the warehouse may vary by type which may have various effects on the players stategy:
	- Some items are heavier that others
	- Some items require specific stacking order
	- Some items may be fragile, and must be placed specifically according to their weight and weight tolerance
	- Some items must be placed in special containers or on special levels
	- Some items might require specific items to be carried without consequences
	- The order that the player gets the items might have an effect on later item placements (the player might need to rearange some items to accomodate new shipments)
- Certain effects might take place in the warehouse
	- Power goes out and the player must go by memory if they do not have a flashlight or other source of light
	- Truck movement outside the warehouse might shake stacked items
	- Various boxes might drip or leave debris that could hinder movement
	- Trucks might be broken which can impede shipments - less shipments -> less boxes -> less money
	- Lost cafeteria keys - Cafeteria closed during shift
	- (Easter egg) Goose honking - Incredibly low chance of a goose honk while the player is carrying a box that makes them drop the box
	- ...
- Each day must be harder than the last
- Every next day may have any combination of effects based on the day number and the effect difficulty
	- eg. The player can't experience an earthquake on the first day
- **`` After enough days have passed, the player gets a promotion and wins the game``**

## Currency & Rewards

- The main currency of the game is `money` expressed in `dollars`
- The player gains money at the end of their shift based on the amount of boxes that are correctly stacked and on the box types themselves
	- The number of boxes gives the player a sort of bonus
	- Each box is valued at a set amount of money based on its type, weight, cargo, etc.
- The amount of money the player gets is always calculated at the end of the shift

## Survival Costs & Upgrades

### Survival Costs
#### Each day there is an amount of money that must be used to pay the bills for the players apartment:
- Electricity: Influences the players 'sanity' - No electricity means less things to do all day - making the player rather bored and grumpy. A bored and grumpy player can't be bothered to work, and has lower stamina the next day
- Water: Influences the players hygiene and health. If water is not payed, the player stinks and is grimey and dirty the next day, which can influence the quality of some goods that need to be clean and fresh. Also, not paying the water bill has a change to make the player sick - no access to clean water can badly influence the players health
- Heat: With no heating, the player has a high risk of being sick the next day - sleeping in a cold room during the night raises the change of catching a cold
- Groceries: Has a fixed _must spend_ amount to keep the player fed. Any less money spent on groceries will make the player weak the next day. Spending no money causes `starvation`. Spending more than enough money on groceries gives the player random buffs the next day
- Starvation: **`If the player does not eat for two days, he will die of starvation the next day and the game will be over`**
- Sickness: When the player is sick, his performance is drastically lower during working hours. 
	- Sickness can be cured by spending additional money on medicine. **Spending money on medicine only increases the chances of being cured the next day**. 
	- The player has a fixed percentage of being cured the next day naturally - without spending money on medicine. 
	- The player has a fixed percentage of getting worse the next day, which is higher than the percentage of being cured. **`The player can get worse a maximum of 4 times, and if he would get worse a fifth time the next day, he will die of sickness and the game will be over`**. Any bills that influence the chances of getting sick that are not payed also increase the chances of getting worse.
	- NOTE: _None of these chances are 100% and cannot ever be 100%._

### Upgrades
#### Any money that is not spent on the survival costs can be spent on various upgrades:
- Tools and equipment:
	- The player can buy various tools and equpiment that will help him with his job
	- Some items have a `'wear and tear' (W&T)` meter that randomly depletes; once depleted, the item breaks and cannot be used again unless bought again
	- Some items can be maintained by spending money (a very small price) in order to increase their `W&T` meter
	- Various gloves, magnets, hydraulic exoskeleton limbs, various boots, harnesses, helmets, tool belts etc.
- Drinks and snacks:
	- The play can buy various drinks and snacks that will grant him buffs during his shift
	- These items are placed in the cafeteria of the warehouse and can be used by the player at any time during his shift or before clocking in
	- These items grant
		- Strictly positive buffs - Something that will increase the performance of the player at no cost
		- Positive/Negative buffs - Something that will increase the performance of the player at a cost (drawback that must be balanced or weaker than the buff)
		- Positive and later negative buff - These items help the player during their duration, and then later have a debuff for a fixed duration, or until the end of the shift
	- Positive buffs are _always_ better than the negative buffs in their combination
		- One item cannot give the player a 'slight speed boost' but hinder their strength and vision and inventory and shorten their shift time etc. all at the same time
- Machines:
	- The player can spend money to invest in machines for the warehouse
		- Machines can never break down - **``Once they are bought, they are available to the player``**
		- Machines, however, run on some sort of fuel
			- Machines deplete fuel as they are used
			- At the start of the shift, machines are completely refueled
			- Some machines may have the ability to be refueled during the shift
			- Fuel level does not affect the machines productivity - it either runs 100% or 0%
			- Fuel can be found/bought for the warehouse
			- Fuel can be: Electricty (generator or battery), Gas (diesel or petrol canisters), Natural gas (natural gas tanks)

# Art & Audio

### Art style
Low poly models with pixelated textures. Items and people should resemble people in the 90s.

1st person perspective camera, 3D models.

Music style can be various, with the music being played on the players radio item (optional). Other music such as menu music can be a calm or liftup style (think: the sims furniture shopping music).


# UI / UX

### HUD (Heads Up Display)
- The players HUD should have the following items (subject to change)
	- Stamina
	- Active effects
	- Any information messages
	- Any instructions that cannot be displayed in the world

### Menus
All menu items that can be manipulated are immediately applied - no reset enabled.
- Main menu
	- Continue story
	- Start new story
		- (If there is an existing story) Are you sure you want to start a new story? [Y/N]
	- Load story
		- Open existing stories
			- Clicking on story loads the game using that story
		- Back
	- Options
		- Controls
			- Keymapping
				- Panel with layout: [Action name - Action Key]
					- Pressing action key halts menu and prompts player to press a key
				- Back
			- Mouse sensitivity (slider)
			- Back
		- Sound
			- Master volume (slider)
			- Voice volume (slider)
			- Ambient volume (slider)
			- Music volume ( slider)
			- Back
		- Video
			- Resolution (dropdown)
			- Any other effects - TBD
			- Back
	- Credits
		- Roll credits as scene / screen
		- Back
	- Exit Game
		- Are you sure you want to exit? [Y/N]
- Game menu
	- Restart Day
		- Are you sure you want to restart the day? [Y/N]
	- Same options as in main menu
	- Main Menu
		- Are you sure you want to quit? (All unsaved progress will be lost)
- In game menus
	- Depending on interaction - TBD

# Technical notes
### Tools
- Unity (latest completely free version)
- Audacity
- Open Shot
- Photoshop
- Blender
- Aseprite

# Scope & Timeline
### Core Features: TBD
### Nice-to-have: TBD
