Mathartic. Render your mathematics!

Enter mathematical expressions to determine the R, G, and B components of each pixel. Expressions can use the built-in parameters x, y, and t, as well as any user-defined parameters added under Extra Param(s). Evaluation is not case-sensitive. Params can reference other params defined before them.

Supported operators include +, -, *, /, = (or ==), !=, <, >, <=, >=, && (or 'and'), || (or 'or'), and the ternary operator ('expression ? true : false'). Supported functions include abs, acos, asin, atan (one- and two-argument forms), ceiling (or 'ceil'), cos, exp, floor, ieeeremainder, log (one- and two-argument forms), log10, pow, round, sign, sin, sqrt, tan, truncate, max, min, and if ('if(expression, true, false)').

Randomized expressions will be provided upon start-up and via the Randomize button. Randomization does not create or remove any extra parameters, but can make use of any already defined.

Example param set:

    r: sin(pow(x,2) + pow(y,2) + t)
    c: r + sin(t) = cos(t)
    s: ieeeremainder(r - atan(y,x), 3.14159 / 4)
