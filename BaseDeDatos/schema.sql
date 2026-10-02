-- Esquema de la prueba Epik (SQLite). Idempotente: la API lo ejecuta al iniciar.
CREATE TABLE IF NOT EXISTS Persona (
    Identificacion TEXT    NOT NULL PRIMARY KEY,
    Nombres        TEXT    NOT NULL,
    Apellidos      TEXT    NOT NULL,
    Edad           INTEGER NOT NULL CHECK (Edad BETWEEN 0 AND 150),
    Genero         TEXT    NOT NULL CHECK (Genero IN ('Masculino', 'Femenino'))
);

CREATE VIEW IF NOT EXISTS VW_Mujeres AS
SELECT Identificacion, Nombres, Apellidos, Edad, Genero
FROM Persona
WHERE Genero = 'Femenino';
