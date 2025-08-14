Какие пользователи сколько уровней прошли
```
SELECT 
    USER_ID,
    COUNT(DISTINCT EVENT_JSON:level_number::integer) AS completed_levels_count
FROM 
    EVENTS
WHERE 
    EVENT_NAME = 'level_complete'
GROUP BY 
    USER_ID
ORDER BY 
    completed_levels_count DESC;

```
