# Main Development Progress and Tracking

This document tracks the progress for the development of the game Boxing Day. The development tracked is for:
- Software development (code, mechanics, tools etc.)
- Artistic development (models, textures, sound effects, music etc.)
- Level design and development (Concrete level design and puzzles, tasks etc.)

# SPECIFICATIONS

- ## Software Development
    - ### Game manager
        A game manager component must exist that can track and access all items in the game for any and all modifications during the game.
        - The game manager must either contain or be contained within all other items in the game
    - ### State management

        - #### Master State manager 
            This state manager tracks and manipulates all possible states the player has
            - Master state manager must have a reference to all other state managers in the game
            - All state managers must have a reference to the master state manager

        - #### Movement state manager
            This state manager controls all movement of the player. 
            - Vertical and horizontal movement
                - Vertical movement is related to climbing up and down ladders and stairs
                - Horizontal movement is related to moving left right forward and backward
            - Jumping
                - Jumping is fixed
                - When the player jumps in a direction, he can not steer while in the air, only look around
            - Changing movement speed through sprinting 
                - Sprinting can be done 

        - #### Interaction state manager
            This state manager controls all the possible interactions the player can have with the world around him.
            - Action button
                - The action button is the main 'portal' from the player to the object he is pointing at
                - This button activates all possible effects the player can have on the outside world
                    - Talking to NPCs
                    - Picking up objects
                    - Accessing truck inventory
                    - Accessing cafeteria containers
                    - Starting interaction with machines
                    - Starting interaction with cafeteria appliances
                    - Reading pallet, shelf, freezer and trunk information 
            - Item rotation and manipulation with the mouse
                - While holding an object, the player must be able to rotate the object using the mouse
            - Item drop button
                - The player must be able to drop an object in front of himself using a dedicated button
            - Item throw button
                - The player must be able to throw an object in front of himself with force using a dedicated button

        - #### Look state manager
            This state manager controls all aspects of the players vision. The state manager is currently a placeholder, in case any more complex vision related mechanics are introduced (zoom in, vision blur).
            - Using the mouse the player must be able to look around himself in all normal look axes


        - #### Player status manager
            This state manager holds all information about the players buffs and debuffs, as well as the players stamina and other status effects.
            - Stamina - Used for increasing movement speed
            - Health (expressed as a debuff if the player is sick)
            - Hunger (expressed as a debuff if the player is malnurished, buff if the player ate well or ate any snacks that give him buffs)
            - Hygiene (expressed as a debuff if the player is dirty, along with other negative effects)
            - Psyche (expressed as a debuff if the player spent the day doing nothing and is bored and grumpy)
            - General positive and negative effects

## Art Development
### TBD

## Level Design
### TBD

# DEVELOPMENT
    
- ### State Manager Implementations
    - [TODO] Master State Manager
    - [TODO] Movement State Manager
    - [TODO] Interaction State Manager
    - [TODO] Look State Manager
    - [TODO] Player Status State Manager