I want to add the functionality to the airflow button. 
There are 2 things which happen when the button is clicked. 
The inlet airflow controller and door toggle controller buttons appear.


The inlet fan pressure can be controlled via inputting a value in the designated gameobject. Its a slider that can be controlled by the mouse wheel or by clicking and dragging it.
The second thing that happens is that the doors can be toggled on/off.
Clicking the airflow button in the UI hides them again. 

I want to add doors to some edges of the graph. When closed, they basically function as debris (infinite resistance). When open, the edge functions as normal.The Door.cs acts as a basic script. Build on it. I should be able to add the door by dragging and dropping into the graphmanager edge objects.
There's a toggle door switch for each door. Its a 3D toggle switch that opens and closes the door when the player clicks on it. 
When clicked on the toggle, it should turn to green if opened, red if closed. 
